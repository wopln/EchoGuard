using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EchoGuard
{
    internal sealed class Identity
    {
        readonly Dictionary<string, Tuple<long, bool>> cache = new Dictionary<string, Tuple<long, bool>>();
        internal bool Verify(uint pid, bool sonar)
        {
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    if (sonar ? p.ProcessName != "SteelSeriesSonar" : p.ProcessName != "Discord")
                        return false;
                    string path = p.MainModule.FileName;
                    var f = new FileInfo(path);
                    string key = path + (sonar ? "|sonar" : "|discord");
                    Tuple<long, bool> hit;
                    if (cache.TryGetValue(key, out hit) && hit.Item1 == f.LastWriteTimeUtc.Ticks)
                        return hit.Item2;
                    bool valid = VerifyFile(path, sonar);
                    cache[key] = Tuple.Create(f.LastWriteTimeUtc.Ticks, valid);
                    return valid;
                }
            }
            catch
            {
                return false;
            }
        }

        internal int DiscordCount()
        {
            int count = 0;
            foreach (var p in Process.GetProcessesByName("Discord"))
            {
                using (p)
                {
                    if (Verify((uint)p.Id, false))
                        count++;
                }
            }

            return count;
        }

        internal static bool OtherHelper()
        {
            using (var current = Process.GetCurrentProcess())
            {
                foreach (var p in Process.GetProcessesByName("AudioInitialization"))
                {
                    using (p)
                    {
                        if (p.SessionId == current.SessionId)
                            return true;
                    }
                }
            }

            return false;
        }

        internal static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "");
            }
        }

        static bool VerifyFile(string path, bool sonar)
        {
            IntPtr name = Marshal.StringToCoTaskMemUni(path), file = IntPtr.Zero;
            try
            {
                FileTrust info = new FileTrust
                {
                    Size = (uint)Marshal.SizeOf(typeof(FileTrust)),
                    Name = name
                };
                file = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileTrust)));
                Marshal.StructureToPtr(info, file, false);
                TrustData data = new TrustData
                {
                    Size = (uint)Marshal.SizeOf(typeof(TrustData)),
                    Ui = 2,
                    Union = 1,
                    File = file,
                    Flags = 0x1010
                };
                Guid action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
                if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0)
                    return false;
                using (var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)))
                {
                    string publisher = cert.GetNameInfo(X509NameType.SimpleName, false);
                    return sonar ? (publisher == "GN Hearing A/S" || publisher == "SteelSeries ApS") : (publisher == "Discord Inc.");
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                Marshal.FreeCoTaskMem(name);
                if (file != IntPtr.Zero)
                    Marshal.FreeHGlobal(file);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FileTrust
        {
            public uint Size;
            public IntPtr Name, Handle, Subject;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TrustData
        {
            public uint Size;
            public IntPtr Policy, Sip;
            public uint Ui, Revocation, Union;
            public IntPtr File;
            public uint State;
            public IntPtr StateData, Url;
            public uint Flags, UiContext;
            public IntPtr Signature;
        }

        [DllImport("wintrust.dll", ExactSpelling = true)]
        static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
    }
}
