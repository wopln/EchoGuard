using System;
using System.Diagnostics;
using System.IO;

internal static class Cleanup
{
    public static int Main(string[] args)
    {
        try
        {
            int pid;
            if (args.Length != 1 || !Int32.TryParse(args[0], out pid))
                return 2;
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    if (!process.WaitForExit(20000))
                        return 3;
                }
            }
            catch (ArgumentException)
            {
            }

            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "EchoGuard");
            if (!Directory.Exists(dir))
                return 0;
            if ((File.GetAttributes(Path.GetDirectoryName(dir)) & FileAttributes.ReparsePoint) != 0)
                return 4;
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                return 4;
            string marker = Path.Combine(dir, ".echoguard-owned");
            if (!File.Exists(marker) || File.ReadAllText(marker) != "EchoGuard managed installation v1")
                return 4;
            string uninstaller = Path.Combine(dir, "Uninstall.exe");
            if (File.Exists(uninstaller))
                File.Delete(uninstaller);
            File.Delete(marker);
            if (Directory.GetFileSystemEntries(dir).Length == 0)
                Directory.Delete(dir);
            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
