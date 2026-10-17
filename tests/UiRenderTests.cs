using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using EchoGuard;
internal static class UiRenderTests
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static void Pump(Window w){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));w.UpdateLayout();}
    static Rect Bounds(FrameworkElement e,FrameworkElement root){return e.TransformToAncestor(root).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight));}
    static FrameworkElement Control(FrameworkElement root,string name){return (FrameworkElement)root.FindName(name);}
    static void Save(FrameworkElement root,string path,double scale){var bitmap=new RenderTargetBitmap((int)Math.Round(root.ActualWidth*scale),(int)Math.Round(root.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);}
    static void VisibleControls(FrameworkElement root,string label){foreach(var name in new[]{"MicrophoneBox","DiagnoseButton","FixButton","StartupBox","HelpButton","SettingsButton","MinimizeButton","CloseButton"}){var r=Bounds(Control(root,name),root);Check(r.Left>=0&&r.Top>=0&&r.Right<=root.ActualWidth+1&&r.Bottom<=root.ActualHeight+1,label+": "+name+" is within the window");}}
    static int Hit(Window w,double x,double y){var pt=w.PointToScreen(new Point(x,y));long packed=((long)(ushort)(short)pt.Y<<16)|(ushort)(short)pt.X;return SendMessage(new System.Windows.Interop.WindowInteropHelper(w).Handle,0x84,IntPtr.Zero,new IntPtr(packed)).ToInt32();}
    [StructLayout(LayoutKind.Sequential)]struct NativeRect{internal int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr handle,out NativeRect rect);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
    [STAThread]public static int Main(string[] args)
    {
        GuardWindow window=null;AudioEngine engine=null;
        try
        {
            string output=args[0];Directory.CreateDirectory(output);
            string state=Path.Combine(Path.GetTempPath(),"EchoGuard-window-qa-"+Guid.NewGuid().ToString("N"));
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            engine=new AudioEngine(state,false,false);Check(engine.Ready.WaitOne(15000),"unchanged audio engine initializes with protection off");
            window=new GuardWindow(engine,new LocalStore(state),false);app.MainWindow=window;window.Show();Pump(window);
            var root=(FrameworkElement)window.Content;var mic=(ComboBox)root.FindName("MicrophoneBox");var fix=(Button)root.FindName("FixButton");
            Check(window.WindowStyle==WindowStyle.None&&!window.AllowsTransparency&&window.SizeToContent==SizeToContent.Manual,"native non-layered borderless window uses manual sizing");
            Check(WindowChrome.GetWindowChrome(window).CaptionHeight>80,"native caption covers the entire actual header");
            Check(WindowChrome.GetWindowChrome(window).ResizeBorderThickness.Left==6,"six-DIP native resize hit borders retained");
            Check(mic.Items.Count==engine.Current.Microphones.Count+1,"all recording choices and Auto retained");
            Check(!fix.IsEnabled&&((TextBlock)root.FindName("StatusDescription")).Text=="Not checked yet.","initial protection remains opt-in and neutral");
            Check(((CheckBox)root.FindName("StartupBox")).IsChecked==LocalStore.StartupEnabled,"actual startup preference remains reflected");
            foreach(var name in new[]{"SettingsButton","MinimizeButton","CloseButton"})Check(WindowChrome.GetIsHitTestVisibleInChrome((IInputElement)root.FindName(name)),name+" remains client-interactive inside the caption");
            Check(Hit(window,700,70)==2,"empty header produces native HTCAPTION");
            var settingsRect=Bounds(Control(root,"SettingsButton"),root);
            Check(Hit(window,settingsRect.X+35,settingsRect.Y+22)==1,"settings produces HTCLIENT instead of caption");
            Check(Hit(window,2,window.ActualHeight/2)==10,"left border produces native HTLEFT");
            Check(Hit(window,window.ActualWidth-2,window.ActualHeight-2)==17,"bottom-right corner produces native resize hit");
            Save(root,Path.Combine(output,"normal.png"),1);
            // Preserve exact original reference presentation at its design size.
            window.Width=1189;window.Height=868;Pump(window);
            var micRect=Bounds(mic,root);var detectRect=Bounds(Control(root,"DiagnoseButton"),root);
            Check(Math.Abs(micRect.X-180)<2&&Math.Abs(micRect.Y-276)<2,"reference-size microphone anchor preserved");
            Check(Math.Abs(detectRect.X-72)<2&&Math.Abs(detectRect.Y-571)<2&&Math.Abs(detectRect.Width-1045)<2,"reference-size primary button geometry preserved");
            Save(root,Path.Combine(output,"reference-size.png"),1);
            foreach(double scale in new[]{1.0,1.25,1.5})Save(root,Path.Combine(output,"render-"+(96*scale).ToString()+"dpi.png"),scale);
            Check(File.Exists(Path.Combine(output,"render-120dpi.png"))&&File.Exists(Path.Combine(output,"render-144dpi.png")),"WPF render targets at 100 125 and 150 percent succeed");
            window.Width=window.MinWidth;window.Height=window.MinHeight;Pump(window);
            VisibleControls(root,"minimum size");
            Check(((ScrollViewer)root.FindName("PanelScroll")).ScrollableHeight<1,"minimum supported size displays actions without scrolling");
            Check(mic.FontSize>=20&&((TextBlock)root.FindName("StatusDescription")).FontSize>=18,"compact typography remains readable without page scaling");
            Save(root,Path.Combine(output,"minimum.png"),1);            mic.IsDropDownOpen=true;Pump(window);
            var popup=(System.Windows.Controls.Primitives.Popup)mic.Template.FindName("PART_Popup",mic);
            Check(Math.Abs(((FrameworkElement)popup.Child).ActualWidth-mic.ActualWidth)<2,"microphone popup width follows the actual selector");
            Check(((ScrollViewer)((Border)popup.Child).Child).HorizontalScrollBarVisibility==ScrollBarVisibility.Disabled,"microphone popup avoids horizontal overflow");mic.IsDropDownOpen=false;
            window.MinHeight=500;window.Height=560;Pump(window);
            var scroll=(ScrollViewer)root.FindName("PanelScroll");
            Check(scroll.ScrollableHeight>0,"constrained work-area layout scrolls instead of shrinking the page");
            Check(Bounds(Control(root,"StartupBox"),root).Bottom<=root.ActualHeight,"constrained work area keeps footer reachable");
            scroll.ScrollToBottom();Pump(window);
            Check(Bounds(fix,root).Bottom<=Bounds(scroll,root).Bottom+1,"scrolling makes the final action reachable on a short display");
            Save(root,Path.Combine(output,"short-display-scroll.png"),1);
            window.MinHeight=670;window.Height=670;Pump(window);            double narrowWidth=mic.ActualWidth,buttonHeight=Control(root,"DiagnoseButton").ActualHeight;
            window.Width=1500;Pump(window);
            Check(mic.ActualWidth>narrowWidth+500,"wide window expands microphone selection instead of a fixed centered page");
            Check(Math.Abs(Control(root,"DiagnoseButton").ActualHeight-buttonHeight)<1,"horizontal growth does not stretch button height");
            VisibleControls(root,"wide size");Save(root,Path.Combine(output,"wide.png"),1);
            window.Width=1080;window.Height=800;Pump(window);Rect normal=new Rect(window.Left,window.Top,window.ActualWidth,window.ActualHeight);
            window.WindowState=WindowState.Maximized;Pump(window);
            var handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;NativeRect maximized;GetWindowRect(handle,out maximized);var work=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            Check(Math.Abs(maximized.Left-work.Left)<=1&&Math.Abs(maximized.Top-work.Top)<=1&&Math.Abs(maximized.Right-work.Right)<=1&&Math.Abs(maximized.Bottom-work.Bottom)<=1,"maximized native bounds match current monitor work area");
            VisibleControls(root,"maximized");Save(root,Path.Combine(output,"maximized.png"),1);
            Check(Math.Abs(mic.ActualWidth-(root.ActualWidth-260))<3,"maximized microphone fills the actual monitor-width layout");
            window.WindowState=WindowState.Minimized;Pump(window);window.ShowWindow();Pump(window);
            Check(window.WindowState==WindowState.Maximized,"reopening a minimized maximized window preserves maximized state");
            window.WindowState=WindowState.Normal;Pump(window);
            Check(Math.Abs(window.ActualWidth-normal.Width)<2&&Math.Abs(window.ActualHeight-normal.Height)<2,"restore recovers previous normal dimensions");
            Check(Math.Abs(window.Left-normal.Left)<2&&Math.Abs(window.Top-normal.Top)<2,"restore recovers previous normal position");
            Save(root,Path.Combine(output,"restored.png"),1);
            ((Button)root.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var settings=app.Windows.Cast<Window>().First(x=>x.Title=="EchoGuard Settings");Check(settings.IsVisible,"Settings still opens");settings.Close();
            ((Button)root.FindName("HelpButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var help=app.Windows.Cast<Window>().First(x=>x.Title=="EchoGuard Help");Check(help.IsVisible,"offline Help still opens");help.Close();
            ((Button)root.FindName("MinimizeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(window);Check(window.WindowState==WindowState.Minimized,"minimize button changes native WindowState");window.ShowWindow();Pump(window);Check(Math.Abs(window.ActualWidth-normal.Width)<2,"taskbar restore preserves normal width");
            ((Button)root.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!window.IsVisible,"close-to-tray retained");window.ShowWindow();Check(window.IsVisible&&engine.PendingChanges==0,"tray reopening performs no audio modifications");
            File.WriteAllText(Path.Combine(output,"window-tests.json"),new JavaScriptSerializer().Serialize(new{Passed=true,Checks=checks,AudioChangesApplied=false,NativeWorkAreaVerified=true,DpiRendering="96/120/144; Windows scaling not changed",Minimum=new{Width=window.MinWidth,Height=window.MinHeight}}));
            Console.WriteLine("Passed "+checks+" WPF window/layout checks");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine("FAIL "+ex.Message);return 1;}
        finally{if(window!=null)window.ExitAll();else if(engine!=null)engine.Dispose();}
    }
}
