using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EchoGuard
{
    internal static class Branding
    {
        static Stream OpenIcon()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoGuard.Icon.ico");
            if (stream == null) throw new InvalidDataException("The application icon resource is missing.");
            return stream;
        }

        internal static ImageSource WindowIcon()
        {
            using (var stream = OpenIcon())
            {
                var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapFrame best = decoder.Frames[0];
                foreach (var frame in decoder.Frames) if (frame.PixelWidth > best.PixelWidth) best = frame;
                best.Freeze();
                return best;
            }
        }

        internal static System.Drawing.Icon TrayIcon()
        {
            using (var stream = OpenIcon())
            using (var icon = new System.Drawing.Icon(stream, 32, 32))
                return (System.Drawing.Icon)icon.Clone();
        }
    }
}
