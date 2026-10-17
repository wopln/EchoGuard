using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace EchoGuard
{
    // Presentation-only native window integration. No audio APIs or preferences.
    internal class ThemedWindow : Window
    {
        readonly WindowChrome chrome;
        double requestedMinWidth, requestedMinHeight;
        WindowState stateBeforeMinimize = WindowState.Normal;
        IntPtr lastMonitor;
        bool monitorRefreshQueued;

        internal static FrameworkElement LoadView(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoGuard." + name))
                return (FrameworkElement)XamlReader.Load(stream);
        }

        internal ThemedWindow()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoGuard.Theme.xaml"))
                Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
            Icon = Branding.WindowIcon();
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            SizeToContent = SizeToContent.Manual;
            AllowsTransparency = false;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            chrome = new WindowChrome
            {
                CaptionHeight = 32,
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(17),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);
            SourceInitialized += delegate
            {
                var handle = new WindowInteropHelper(this).Handle;
                HwndSource.FromHwnd(handle).AddHook(WindowMessages);
                int dark = 1, rounded = 2;
                DwmSetWindowAttribute(handle, 20, ref dark, 4);
                DwmSetWindowAttribute(handle, 33, ref rounded, 4);
                RefreshMinimumBounds();
            };
            StateChanged += delegate
            {
                if (WindowState != WindowState.Minimized) stateBeforeMinimize = WindowState;
                var root = Content as FrameworkElement;
                var outline = root == null ? null : root.FindName("WindowOutline") as Border;
                if (outline != null)
                    outline.CornerRadius = new CornerRadius(WindowState == WindowState.Maximized ? 0 : 17);
            };
        }

        internal void SetDimensions(double width, double height, double minWidth, double minHeight)
        {
            requestedMinWidth = minWidth;
            requestedMinHeight = minHeight;
            var area = SystemParameters.WorkArea;
            MinWidth = Math.Max(320, Math.Min(minWidth, area.Width - 24));
            MinHeight = Math.Max(320, Math.Min(minHeight, area.Height - 24));
            Width = Math.Max(MinWidth, Math.Min(width, area.Width - 32));
            Height = Math.Max(MinHeight, Math.Min(height, area.Height - 32));
        }

        internal void ConnectDrag(FrameworkElement header)
        {
            // Let Windows own non-client dragging, double-click and Snap. No mouse capture loop.
            Action update = delegate
            {
                if (!header.IsLoaded || header.ActualHeight <= 0) return;
                double bottom = header.TranslatePoint(new Point(0, header.ActualHeight), this).Y;
                double caption = Math.Max(0, bottom - chrome.ResizeBorderThickness.Top);
                if (Math.Abs(chrome.CaptionHeight - caption) > .5) chrome.CaptionHeight = caption;
            };
            header.Loaded += delegate { MarkCaptionControls(header); update(); };
            header.LayoutUpdated += delegate { update(); };
        }

        static void MarkCaptionControls(DependencyObject parent)
        {
            if (parent is ButtonBase || parent is TextBoxBase || parent is Selector)
            {
                WindowChrome.SetIsHitTestVisibleInChrome((IInputElement)parent, true);
                return;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                MarkCaptionControls(VisualTreeHelper.GetChild(parent, i));
        }

        internal void RevealWindow()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = stateBeforeMinimize;
            Activate();
        }

        void RefreshMinimumBounds()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var monitor = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            lastMonitor = MonitorFromWindow(handle, 2);
            if (!GetMonitorInfo(lastMonitor, ref monitor)) return;
            var source = HwndSource.FromHwnd(handle);
            if (source == null || source.CompositionTarget == null) return;
            var transform = source.CompositionTarget.TransformFromDevice;
            var work = transform.Transform(new Vector(monitor.Work.Right - monitor.Work.Left, monitor.Work.Bottom - monitor.Work.Top));
            MinWidth = Math.Max(320, Math.Min(requestedMinWidth, work.X - 24));
            MinHeight = Math.Max(320, Math.Min(requestedMinHeight, work.Y - 24));
        }

        IntPtr WindowMessages(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0024) // WM_GETMINMAXINFO: native monitor work area, including non-primary monitors.
            {
                var monitor = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor))
                {
                    var bounds = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
                    bounds.MaxPosition.X = monitor.Work.Left - monitor.Monitor.Left;
                    bounds.MaxPosition.Y = monitor.Work.Top - monitor.Monitor.Top;
                    bounds.MaxSize.X = monitor.Work.Right - monitor.Work.Left;
                    bounds.MaxSize.Y = monitor.Work.Bottom - monitor.Work.Top;
                    Marshal.StructureToPtr(bounds, lParam, false);
                    handled = true;
                }
            }
            else if (message == 0x02E0 || message == 0x007E) // DPI/display changes; WPF owns WM_DPICHANGED positioning.
                QueueMonitorRefresh();
            else if (message == 0x0047 && MonitorFromWindow(handle, 2) != lastMonitor)
                QueueMonitorRefresh();
            return IntPtr.Zero;
        }

        void QueueMonitorRefresh()
        {
            if (monitorRefreshQueued) return;
            monitorRefreshQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                monitorRefreshQueued = false;
                RefreshMinimumBounds();
            }));
        }
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo { internal NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
        [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { internal int Size; internal NativeRect Monitor, Work; internal uint Flags; }
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
    }
}
