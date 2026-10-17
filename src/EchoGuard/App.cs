using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace EchoGuard
{
    internal static class Entry
    {
        internal static string StatePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoGuard");
            }
        }

        internal static string UserKey
        {
            get
            {
                return Identity.Hash(WindowsIdentity.GetCurrent().User.Value).Substring(0, 20);
            }
        }

        internal static string MutexName
        {
            get
            {
                return @"Local\EchoGuard.Instance." + UserKey;
            }
        }

        internal static string RestoreName
        {
            get
            {
                return @"Local\EchoGuard.Restore." + UserKey;
            }
        }

        internal static string ShowName
        {
            get
            {
                return @"Local\EchoGuard.Show." + UserKey;
            }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0] == "--diagnose")
                {
                    using (var engine = new AudioEngine(StatePath, false, true))
                    {
                        if (!engine.Ready.WaitOne(15000))
                            return 2;
                        File.WriteAllText(args[1], engine.Current.Summary());
                    }

                    return 0;
                }

                if (args.Length == 1 && args[0] == "--restore-and-exit")
                {
                    bool found = false;
                    try
                    {
                        using (var signal = EventWaitHandle.OpenExisting(RestoreName))
                        {
                            signal.Set();
                            found = true;
                        }
                    }
                    catch (WaitHandleCannotBeOpenedException)
                    {
                    }

                    if (found)
                    {
                        try
                        {
                            using (var m = Mutex.OpenExisting(MutexName))
                            {
                                try
                                {
                                    if (!m.WaitOne(15000))
                                        return 3;
                                }
                                catch (AbandonedMutexException)
                                {
                                }

                                m.ReleaseMutex();
                            }
                        }
                        catch (WaitHandleCannotBeOpenedException)
                        {
                        }

                        var recovery = new LocalStore(StatePath);
                        var records = recovery.LoadJournal();
                        return recovery.InvalidJournal || records.Count > 0 ? 3 : 0;
                    }

                    var restoreEngine = new AudioEngine(StatePath, false, false);
                    restoreEngine.Ready.WaitOne(15000);
                    restoreEngine.Dispose();
                    return restoreEngine.RestorationIncomplete ? 3 : 0;
                }

                bool first;
                using (var mutex = new Mutex(true, MutexName, out first))
                {
                    if (!first)
                    {
                        try
                        {
                            using (var show = EventWaitHandle.OpenExisting(ShowName))
                                show.Set();
                        }
                        catch
                        {
                        }

                        return 0;
                    }

                    using (var restore = new EventWaitHandle(false, EventResetMode.AutoReset, RestoreName))
                    using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowName))
                    {
                        var app = new Application
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown
                        };
                        var store = new LocalStore(StatePath);
                        var prefs = store.LoadPreferences();
                        var engine = new AudioEngine(StatePath, prefs.ProtectionEnabled, false);
                        var window = new GuardWindow(engine, store, prefs.ProtectionEnabled);
                        app.MainWindow = window;
                        ThreadPool.RegisterWaitForSingleObject(restore, delegate
                        {
                            app.Dispatcher.BeginInvoke(new Action(window.ExitAll));
                        }, null, Timeout.Infinite, false);
                        ThreadPool.RegisterWaitForSingleObject(show, delegate
                        {
                            app.Dispatcher.BeginInvoke(new Action(window.ShowWindow));
                        }, null, Timeout.Infinite, false);
                        if (args.Length == 0 || args[0] != "--tray")
                            window.Show();
                        int code = app.Run();
                        mutex.ReleaseMutex();
                        return code;
                    }
                }
            }
            catch (Exception ex)
            {
                if (args.Length > 0)
                {
                    new LocalStore(StatePath).Log("Command failed: " + ex.GetType().Name);
                    return 1;
                }

                MessageBox.Show("EchoGuard could not start safely.\n" + ex.GetType().Name + " (0x" + System.Runtime.InteropServices.Marshal.GetHRForException(ex).ToString("X8") + ")", "EchoGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return 1;
            }
        }
    }

    internal sealed class MicrophoneOption
    {
        public string Id { get; set; }
        public string Label { get; set; }

        public bool Missing;
        public override string ToString()
        {
            return Label;
        }
    }


    internal sealed class GuardWindow : ThemedWindow
    {
        readonly AudioEngine engine;readonly LocalStore store;readonly Forms.NotifyIcon tray;readonly System.Drawing.Icon trayImage;
        readonly ComboBox microphoneBox;readonly Button fix;readonly TextBlock status,description,fixLabel;readonly System.Windows.Shapes.Ellipse dot;readonly CheckBox startup;
        bool protection,exiting,updatingSelection,checkedByUser,analyzing;Diagnosis current;
        ThemedWindow settings,help;FrameworkElement settingsView;string actionMessage="Selecting a microphone does not apply a fix or change Windows/Discord input settings.";
        internal GuardWindow(AudioEngine audio,LocalStore local,bool enabled){
            engine=audio;store=local;protection=enabled;Title="EchoGuard";SetDimensions(1189,868,850,670);ApplyResponsiveSpacing();SizeChanged+=delegate{ApplyResponsiveSpacing();};
            Content=LoadView("MainWindow.xaml");var grid=(FrameworkElement)Content;
            microphoneBox=(ComboBox)grid.FindName("MicrophoneBox");fix=(Button)grid.FindName("FixButton");fixLabel=(TextBlock)grid.FindName("FixLabel");status=(TextBlock)grid.FindName("StatusText");description=(TextBlock)grid.FindName("StatusDescription");dot=(System.Windows.Shapes.Ellipse)grid.FindName("StatusDot");startup=(CheckBox)grid.FindName("StartupBox");
            ConnectDrag((FrameworkElement)grid.FindName("DragHeader"));
            ((Button)grid.FindName("MinimizeButton")).Click+=delegate{WindowState=WindowState.Minimized;};
            ((Button)grid.FindName("CloseButton")).Click+=delegate{Close();};
            ((Button)grid.FindName("SettingsButton")).Click+=delegate{OpenSettings();};
            ((Button)grid.FindName("HelpButton")).Click+=delegate{OpenHelp();};
            ((Button)grid.FindName("DiagnoseButton")).Click+=delegate{BeginDiagnosis();};
            microphoneBox.SelectionChanged+=delegate{
                if(updatingSelection)return;var option=microphoneBox.SelectedItem as MicrophoneOption;if(option==null||option.Missing)return;
                checkedByUser=false;analyzing=false;fix.IsEnabled=false;engine.SelectMicrophone(option.Id);actionMessage="Selection changed; protection is disabled. Detect Echo before applying a fix.";
            };
            fix.Click+=delegate{fix.IsEnabled=false;actionMessage=protection?"Restoring only owned session changes...":"Checking the supported route before applying protection...";engine.SetEnabled(!protection);};
            startup.IsChecked=LocalStore.StartupEnabled;startup.Click+=delegate{engine.StartupPreference(startup.IsChecked==true);};
            trayImage=Branding.TrayIcon();tray=new Forms.NotifyIcon{Icon=trayImage,Visible=true,Text="EchoGuard - checking audio"};var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("Open EchoGuard",null,delegate{Dispatcher.BeginInvoke(new Action(ShowWindow));});
            menu.Items.Add("Run diagnostics",null,delegate{Dispatcher.BeginInvoke(new Action(BeginDiagnosis));});
            menu.Items.Add("Disable and restore",null,delegate{Dispatcher.BeginInvoke(new Action(Restore));});
            menu.Items.Add("Settings",null,delegate{Dispatcher.BeginInvoke(new Action(OpenSettings));});
            menu.Items.Add("Exit completely",null,delegate{Dispatcher.BeginInvoke(new Action(ExitAll));});tray.ContextMenuStrip=menu;
            tray.DoubleClick+=delegate{Dispatcher.BeginInvoke(new Action(ShowWindow));};
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!exiting){e.Cancel=true;if(settings!=null)settings.Hide();if(help!=null)help.Hide();Hide();}};
            engine.Changed+=delegate(Diagnosis d){Dispatcher.BeginInvoke(new Action(delegate{analyzing=false;Update(d);}));};Update(engine.Current);
        }
        bool? compactSpacing;
        void ApplyResponsiveSpacing()
        {
            bool compact = (ActualHeight > 0 ? ActualHeight : Height) < 820;
            if (compactSpacing == compact) return;
            compactSpacing = compact;
            Resources["PageMargin"] = new Thickness(compact ? 24 : 35);
            Resources["HeaderHeight"] = new GridLength(compact ? 95 : 115);
            Resources["FooterHeight"] = new GridLength(compact ? 50 : 59);
            Resources["CaptionButtonsMargin"] = compact ? new Thickness(0,-12,-18,0) : new Thickness(0,-23,-29,0);
            Resources["PanelPadding"] = compact ? new Thickness(18) : new Thickness(35,40,35,29);
            Resources["SectionGap"] = new GridLength(compact ? 18 : 40);
            Resources["ActionGap"] = new GridLength(compact ? 18 : 29);
            Resources["DetectHeight"] = new GridLength(compact ? 62 : 79);
            Resources["FixHeight"] = new GridLength(compact ? 62 : 78);
            Resources["ButtonGap"] = new GridLength(compact ? 12 : 15);
            Resources["IconColumn"] = new GridLength(compact ? 90 : 108);
            Resources["HeadingHeight"] = compact ? 30.0 : 34.0;
            Resources["BodyHeight"] = compact ? 25.0 : 27.0;
            Resources["BodyMargin"] = new Thickness(0,compact ? 4 : 6,0,0);
            Resources["ChoiceHeight"] = compact ? 52.0 : 63.0;
            Resources["ChoiceMargin"] = new Thickness(0,compact ? 10 : 17,0,0);
            Resources["StatusHeight"] = compact ? 64.0 : 80.0;
            Resources["StatusMargin"] = new Thickness(0,compact ? 10 : 16,0,0);
            Resources["SectionFont"] = compact ? 23.0 : 24.5;
            Resources["StatusHeadingFont"] = compact ? 23.0 : 25.0;
            Resources["BodyFont"] = compact ? 18.0 : 20.2;
            Resources["StatusBodyFont"] = compact ? 18.0 : 20.0;
            Resources["ChoiceFont"] = compact ? 20.0 : 24.0;
            Resources["StatusMessageFont"] = compact ? 19.0 : 21.3;
            Resources["FooterMargin"] = new Thickness(1,compact ? 17 : 26,0,0);
        }
        void BeginDiagnosis(){checkedByUser=true;analyzing=true;RefreshStatus();engine.RunDiagnostics();}
        void Restore(){checkedByUser=true;analyzing=false;actionMessage="Restoring only EchoGuard-owned mute changes.";engine.Restore();}
        void Update(Diagnosis d){
            current=d;protection=d.ProtectionEnabled;updatingSelection=true;
            try{
                var choices=new System.Collections.Generic.List<MicrophoneOption>{new MicrophoneOption{Label="Auto Detect (Recommended)"}};
                foreach(var mic in d.Microphones)choices.Add(new MicrophoneOption{Id=mic.Id,Label=mic.DisplayLabel});
                string selected=d.ManualMode?(d.Selection.Microphone==null?(d.SavedMicrophone==null?"":d.SavedMicrophone.EndpointId):d.Selection.Microphone.Id):null;
                if(d.ManualMode&&!choices.Exists(c=>c.Id==selected))choices.Add(new MicrophoneOption{Id=selected,Label=(d.SavedMicrophone==null?"Saved microphone":d.SavedMicrophone.Name)+" · Unavailable",Missing=true});
                // Preserve an open native dropdown during unrelated audio notifications.
                var old=microphoneBox.ItemsSource as System.Collections.Generic.List<MicrophoneOption>;
                bool same=old!=null&&old.Count==choices.Count; if(same)for(int i=0;i<old.Count;i++)if(old[i].Id!=choices[i].Id||old[i].Label!=choices[i].Label){same=false;break;}
                if(!same){microphoneBox.ItemsSource=choices;old=choices;}
                microphoneBox.SelectedItem=old.Find(c=>c.Id==selected);startup.IsChecked=LocalStore.StartupEnabled;
            }finally{updatingSelection=false;}
            RefreshStatus();UpdateSettings();tray.Text="EchoGuard - "+d.Status;
        }
        void RefreshStatus(){if(current==null)return;var p=StatusPresentation.From(current,checkedByUser,analyzing);description.Text=p.Description;status.Text=p.Message;status.ToolTip=current.Detail+"\n"+current.DuplicatePath;dot.Fill=(Brush)new BrushConverter().ConvertFromString(p.Color);dot.Stroke=dot.Fill;fix.IsEnabled=p.CanFix;fixLabel.Text=protection?"Disable Protection":"Fix Echo";fix.ToolTip=protection?"Restore only EchoGuard-owned changes.":"Available only after a supported diagnosis.";}
        void OpenSettings(){
            if(settings!=null){settings.Show();settings.Activate();return;}
            settings=new ThemedWindow{Title="EchoGuard Settings",Owner=this};settings.SetDimensions(820,780,600,420);settingsView=LoadView("SettingsWindow.xaml");settings.Content=settingsView;settings.ConnectDrag((FrameworkElement)settingsView.FindName("DialogHeader"));
            ((Button)settingsView.FindName("DialogCloseButton")).Click+=delegate{settings.Close();};
            ((CheckBox)settingsView.FindName("RememberBox")).Click+=delegate{engine.RememberConfiguration(((CheckBox)settingsView.FindName("RememberBox")).IsChecked==true);};
            ((Button)settingsView.FindName("RestoreButton")).Click+=delegate{Restore();};
            ((Button)settingsView.FindName("SettingsDiagnoseButton")).Click+=delegate{BeginDiagnosis();};
            ((Button)settingsView.FindName("ExportButton")).Click+=delegate{ExportReport();};
            ((Button)settingsView.FindName("ExitButton")).Click+=delegate{ExitAll();};
            settings.Closed+=delegate{settings=null;settingsView=null;};UpdateSettings();settings.Show();
        }
        void UpdateSettings(){if(settingsView==null||current==null)return;var d=current;
            ((TextBlock)settingsView.FindName("SelectionText")).Text=d.Selection.Confidence+" · "+d.Selection.Resolution+"\n"+(d.Selection.Microphone==null?"":d.Selection.Microphone.Name+"\n")+d.Selection.Explanation;
            ((TextBlock)settingsView.FindName("ProcessingText")).Text=d.Processing;((TextBlock)settingsView.FindName("SourceText")).Text=d.DuplicatePath;
            ((TextBlock)settingsView.FindName("CompatibilityText")).Text="Protection compatibility: "+d.Compatibility;
            ((CheckBox)settingsView.FindName("RememberBox")).IsChecked=d.RememberConfiguration;((ListBox)settingsView.FindName("MicrophoneList")).ItemsSource=d.Microphones;
            ((TextBox)settingsView.FindName("DiagnosticsText")).Text=d.Summary()+"\n"+d.Detail;((TextBlock)settingsView.FindName("ActionText")).Text=actionMessage;
        }
        void ExportReport(){var save=new Microsoft.Win32.SaveFileDialog{Filter="Text report|*.txt",FileName="EchoGuard-diagnostics.txt"};if(save.ShowDialog(settings)==true){try{File.WriteAllText(save.FileName,engine.Current.Summary());actionMessage="Redacted local report saved.";}catch{actionMessage="Windows could not save the report to that location.";}UpdateSettings();}}
        void OpenHelp(){
            if(help!=null){help.Show();help.Activate();return;}help=new ThemedWindow{Title="EchoGuard Help",Owner=this};help.SetDimensions(760,650,560,420);
            var root=new System.Windows.Controls.Grid{Margin=new Thickness(28)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
            var header=new Grid{Margin=new Thickness(0,0,0,20),Background=Brushes.Transparent};header.Children.Add(new TextBlock{Text="Need help?",FontSize=28,FontWeight=FontWeights.SemiBold});
            var close=new Button{Width=38,Height=38,HorizontalAlignment=HorizontalAlignment.Right,Content="×",FontSize=26,Style=(Style)help.FindResource("IconButton")};close.Click+=delegate{help.Close();};header.Children.Add(close);help.ConnectDrag(header);root.Children.Add(header);
            string text="EchoGuard protects one known Sonar / Discord Entire Screen microphone playback path.\n\n1. Select Auto Detect or the processed Sonar microphone used by Discord. Choosing an input changes neither Windows nor Discord.\n\n2. Join voice, start Entire Screen sharing with audio, then choose Detect Echo. Fix Echo is enabled only for a supported, verified path.\n\n3. Protection Active confirms native session mute, not audible sound. Check voice once and game/music from another device with headphones. Avoid playing your stream on the sharing PC.\n\n4. Settings contains input confidence, device identities, diagnostics, Remember, report export, restoration and Exit completely.\n\nAnother AudioInitialization helper blocks exclusions to prevent overlap. Your personal defaults, microphone levels and presets are not replaced.\n\nClose hides EchoGuard in the tray. Minimize uses the taskbar. Disable Protection or Restore previous state undoes only owned changes; keep recovery records if restoration needs attention.\n\nOptional login startup is the main window checkbox. EchoGuard 1.0.0 is unsigned and does not support arbitrary mixers or generic acoustic echo.";
            var body=new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=(Brush)help.FindResource("SecondaryText"),FontSize=17,Padding=new Thickness(0,0,12,0)};Grid.SetRow(body,1);root.Children.Add(body);
            help.Content=new Border{Background=(Brush)help.FindResource("WindowSurface"),BorderBrush=(Brush)help.FindResource("BorderBrush"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Child=root};help.Closed+=delegate{help=null;};help.Show();
        }
        internal void ShowWindow(){RevealWindow();}
        internal void ExitAll(){if(exiting)return;exiting=true;try{engine.Dispose();tray.Dispose();trayImage.Dispose();if(settings!=null)settings.Close();if(help!=null)help.Close();if(engine.RestorationIncomplete){MessageBox.Show(this,"Some session changes could not be restored. The local recovery record was retained. Reopen EchoGuard when the audio devices are available.","Restoration requires attention",MessageBoxButton.OK,MessageBoxImage.Warning);Application.Current.Shutdown(2);}else Application.Current.Shutdown();}catch{exiting=false;MessageBox.Show(this,"Restoration did not finish. Keep EchoGuard open and run diagnostics before removing its files.","EchoGuard",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    }
}


