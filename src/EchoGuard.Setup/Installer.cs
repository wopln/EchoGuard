using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EchoGuard.Setup
{
    internal static class Installer
    {
        internal static string InstallDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "EchoGuard");
            }
        }

        internal static string StateDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoGuard");
            }
        }

        static readonly string[] DocNames =
        {
            "ARCHITECTURE.md",
            "DELIVERY_VALIDATION.md",
            "SMART_DETECTION.md",
            "TESTING.md",
            "TROUBLESHOOTING.md"
        };
        static readonly string[] RootDocs =
        {
            "PRIVACY.md",
            "SUPPORTED_CONFIGURATIONS.md",
            "CHANGELOG.md"
        };
        const string Marker = "EchoGuard managed installation v1";
        [STAThread]
        public static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            if (args.Length > 0 && args[0] == "--install")
            {
                try
                {
                    Install();
                    return 0;
                }
                catch
                {
                    return 1;
                }
            }

            if (args.Length > 0 && args[0] == "--uninstall-quiet")
            {
                try
                {
                    Uninstall();
                    return 0;
                }
                catch
                {
                    return 1;
                }
            }

            if (args.Length > 0 && args[0] == "--uninstall")
            {
                try
                {
                    Uninstall();
                    MessageBox.Show("EchoGuard has been removed. Its owned audio-session changes were restored.", "EchoGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "EchoGuard uninstall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 1;
                }
            }

            var form = new Form
            {
                Text = "EchoGuard Setup",
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Width = 540,
                Height = 325,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                BackColor = System.Drawing.Color.FromArgb(13, 18, 27),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 10)
            };
            var heading = new Label
            {
                Text = "EchoGuard",
                Font = new System.Drawing.Font("Segoe UI", 22, System.Drawing.FontStyle.Bold),
                Left = 24,
                Top = 22,
                Width = 450,
                Height = 42
            };
            var text = new Label
            {
                Text = "EchoGuard 1.0.0\nInstall for your Windows account. No administrator access.\nProtection and login startup are off for a new installation.",
                Left = 26,
                Top = 80,
                Width = 470,
                Height = 85
            };
            var open = new CheckBox
            {
                Text = "Open EchoGuard after installation",
                Left = 26,
                Top = 169,
                Width = 440,
                Checked = true
            };
            var install = new Button
            {
                Text = "Install for me",
                Left = 26,
                Top = 216,
                Width = 145,
                Height = 38,
                BackColor = System.Drawing.Color.FromArgb(22, 119, 255),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            var remove = new Button
            {
                Text = "Uninstall",
                Left = 183,
                Top = 216,
                Width = 145,
                Height = 38,
                FlatStyle = FlatStyle.Flat
            };
            var close = new Button
            {
                Text = "Close",
                Left = 340,
                Top = 216,
                Width = 145,
                Height = 38,
                FlatStyle = FlatStyle.Flat
            };
            form.Controls.AddRange(new Control[] { heading, text, open, install, remove, close });
            close.Click += delegate
            {
                form.Close();
            };
            install.Click += delegate
            {
                try
                {
                    Install();
                    text.Text = "Installed successfully.\nOpen EchoGuard and run diagnostics before enabling protection.";
                    install.Enabled = false;
                    if (open.Checked)
                        Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, "EchoGuard.exe")) { UseShellExecute = true });
                }
                catch (Exception e)
                {
                    MessageBox.Show(form, e.Message, "Installation did not complete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            remove.Click += delegate
            {
                try
                {
                    Uninstall();
                    form.Close();
                }
                catch (Exception e)
                {
                    MessageBox.Show(form, e.Message, "Uninstall needs attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            Application.Run(form);
            return 0;
        }

        static void CheckOwner()
        {
            string mark = Path.Combine(InstallDir, ".echoguard-owned");
            if (!File.Exists(mark) || File.ReadAllText(mark) != Marker)
                throw new InvalidOperationException("The destination is not a recognized EchoGuard installation. No files were removed or overwritten.");
        }

        static void StopAndRestore()
        {
            string exe = Path.Combine(InstallDir, "EchoGuard.exe");
            if (!File.Exists(exe))
                return;
            var p = Process.Start(new ProcessStartInfo(exe, "--restore-and-exit") { UseShellExecute = false, CreateNoWindow = true });
            using (p)
            {
                if (!p.WaitForExit(20000) || p.ExitCode != 0)
                    throw new InvalidOperationException("EchoGuard did not finish restoring audio. Close it using Exit completely, then retry.");
            }
        }

        static void Extract(string resource, string name)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (input == null)
                    throw new InvalidDataException("Installer payload is incomplete.");
                using (var output = File.Create(Path.Combine(InstallDir, name)))
                    input.CopyTo(output);
            }
        }

        static void CheckExistingRegistration()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EchoGuard"))
                if (k != null && !String.Equals(Convert.ToString(k.GetValue("InstallLocation")), InstallDir, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Another installation owns the EchoGuard registration. It was preserved.");
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "EchoGuard.lnk");
            if (!File.Exists(path))
                return;
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                object link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                try
                {
                    string target = Convert.ToString(link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null));
                    if (!String.Equals(target, Path.Combine(InstallDir, "EchoGuard.exe"), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("An unrelated Start shortcut has the same name. It was preserved.");
                }
                finally
                {
                    Marshal.ReleaseComObject(link);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }

        static void Install()
        {
            CheckSafeDirectory(InstallDir);
            CheckSafeDirectory(Path.GetDirectoryName(InstallDir));
            CheckExistingRegistration();
            CheckSafeDirectory(Path.Combine(InstallDir, "docs"));
            CheckSafeDirectory(Path.Combine(InstallDir, "docs", "images"));
            if (Directory.Exists(InstallDir) && Directory.GetFileSystemEntries(InstallDir).Length > 0)
                CheckOwner();
            StopAndRestore();
            Directory.CreateDirectory(InstallDir);
            File.WriteAllText(Path.Combine(InstallDir, ".echoguard-owned"), Marker);
            Extract("Payload.EchoGuard.exe", "EchoGuard.exe");
            Extract("Payload.Config", "EchoGuard.exe.config");
            Extract("Payload.License", "LICENSE.txt");
            Extract("Payload.Readme", "README.md");
            Directory.CreateDirectory(Path.Combine(InstallDir, "docs"));
            Directory.CreateDirectory(Path.Combine(InstallDir, "docs", "images"));
            Extract("Payload.Screenshot", Path.Combine("docs", "images", "app.png"));
            foreach (string name in DocNames)
                Extract("Payload.Doc." + name, Path.Combine("docs", name));
            foreach (string name in RootDocs)
                Extract("Payload.Root." + name, name);
            string uninstall = Path.Combine(InstallDir, "Uninstall.exe");
            if (!String.Equals(Assembly.GetExecutingAssembly().Location, uninstall, StringComparison.OrdinalIgnoreCase))
                File.Copy(Assembly.GetExecutingAssembly().Location, uninstall, true);
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EchoGuard"))
            {
                k.SetValue("DisplayName", "EchoGuard");
                k.SetValue("DisplayVersion", "1.0.0");
                k.SetValue("Publisher", "EchoGuard contributors");
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("DisplayIcon", Path.Combine(InstallDir, "EchoGuard.exe"));
                k.SetValue("UninstallString", "\"" + uninstall + "\" --uninstall");
                k.SetValue("NoModify", 1);
                k.SetValue("NoRepair", 1);
            }

            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "EchoGuard.lnk");
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                object link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcut });
                try
                {
                    link.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { Path.Combine(InstallDir, "EchoGuard.exe") });
                    link.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { InstallDir });
                    link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                }
                finally
                {
                    Marshal.ReleaseComObject(link);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }

        static void CheckSafeDirectory(string path)
        {
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("A redirected installation or settings directory is not supported.");
        }

        static void Uninstall()
        {
            if (!Directory.Exists(InstallDir))
                return;
            CheckSafeDirectory(InstallDir);
            CheckSafeDirectory(Path.GetDirectoryName(InstallDir));
            CheckSafeDirectory(StateDir);
            CheckSafeDirectory(Path.Combine(InstallDir, "docs"));
            CheckSafeDirectory(Path.Combine(InstallDir, "docs", "images"));
            CheckOwner();
            StopAndRestore();
            string journal = Path.Combine(StateDir, "changes.json");
            if (File.Exists(journal))
            {
                try
                {
                    var records = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(journal));
                    if (records == null || records.Count > 0)
                        throw new InvalidDataException();
                }
                catch
                {
                    throw new InvalidOperationException("Audio restoration is not confirmed. Reopen EchoGuard and use Restore previous state before uninstalling. The recovery record was preserved.");
                }
            }

            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (k != null)
                {
                    string value = k.GetValue("EchoGuard") as string;
                    string expected = "\"" + Path.Combine(InstallDir, "EchoGuard.exe") + "\"";
                    if (value != null && (value == expected || value.StartsWith(expected + " ", StringComparison.OrdinalIgnoreCase)))
                        k.DeleteValue("EchoGuard", false);
                }
            }

            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EchoGuard"))
            {
                if (k != null && Convert.ToString(k.GetValue("InstallLocation")) != InstallDir)
                    throw new InvalidOperationException("The uninstall registration belongs to another location; it was preserved.");
            }

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EchoGuard", false);
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "EchoGuard.lnk");
            if (File.Exists(shortcut))
            {
                object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                try
                {
                    object link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcut });
                    try
                    {
                        string target = Convert.ToString(link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null));
                        if (String.Equals(target, Path.Combine(InstallDir, "EchoGuard.exe"), StringComparison.OrdinalIgnoreCase))
                            File.Delete(shortcut);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(link);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }

            foreach (string name in new[]
            {
                "EchoGuard.exe",
                "EchoGuard.exe.config",
                "LICENSE.txt",
                "README.md"
            }

            )
            {
                string path = Path.Combine(InstallDir, name);
                if (File.Exists(path))
                    File.Delete(path);
            }

            foreach (string name in RootDocs)
            {
                string path = Path.Combine(InstallDir, name);
                if (File.Exists(path))
                    File.Delete(path);
            }

            string docs = Path.Combine(InstallDir, "docs");
            foreach (string name in DocNames)
            {
                string path = Path.Combine(docs, name);
                if (File.Exists(path))
                    File.Delete(path);
            }

            string images = Path.Combine(docs, "images");
            string screenshot = Path.Combine(images, "app.png");
            if (File.Exists(screenshot)) File.Delete(screenshot);
            if (Directory.Exists(images) && Directory.GetFileSystemEntries(images).Length == 0) Directory.Delete(images);
            if (Directory.Exists(docs) && Directory.GetFileSystemEntries(docs).Length == 0)
                Directory.Delete(docs);
            CheckSafeDirectory(StateDir);
            foreach (string name in new[]
            {
                "settings.json",
                "changes.json",
                "events.log",
                "events.log.previous",
                "settings.json.new",
                "changes.json.new"
            }

            )
            {
                string path = Path.Combine(StateDir, name);
                if (File.Exists(path))
                    File.Delete(path);
            }

            if (Directory.Exists(StateDir) && Directory.GetFileSystemEntries(StateDir).Length == 0)
                Directory.Delete(StateDir);
            string cleanup = Path.Combine(Path.GetTempPath(), "EchoGuard-cleanup-" + Guid.NewGuid().ToString("N") + ".exe");
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.Cleanup"))
            using (var output = File.Create(cleanup))
            {
                input.CopyTo(output);
            }

            Process.Start(new ProcessStartInfo(cleanup, Process.GetCurrentProcess().Id.ToString()) { UseShellExecute = false, CreateNoWindow = true });
        }
    }
}


