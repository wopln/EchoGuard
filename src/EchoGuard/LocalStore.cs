using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace EchoGuard
{
    public sealed class Preferences
    {
        public bool ProtectionEnabled, StartWithWindows;
        public bool RememberConfiguration = true;
        public string SelectionMode = "Auto", ProcessingSystem = "Automatic", EchoSource = "Automatic";
        public DeviceIdentity SelectedMicrophone;
        public ValidatedConfiguration LastValidated;
    }

    public sealed class Change
    {
        public bool OriginalMute;
        public bool ExternalOverride;
    }

    internal sealed class LocalStore
    {
        internal readonly string DirectoryPath;
        internal bool InvalidJournal;
        readonly JavaScriptSerializer json = new JavaScriptSerializer
        {
            MaxJsonLength = 262144
        };
        internal LocalStore(string path)
        {
            DirectoryPath = path;
            Directory.CreateDirectory(path);
        }

        internal Preferences LoadPreferences()
        {
            try
            {
                return json.Deserialize<Preferences>(File.ReadAllText(Path.Combine(DirectoryPath, "settings.json"))) ?? new Preferences();
            }
            catch
            {
                return new Preferences();
            }
        }

        internal Dictionary<string, Change> LoadJournal()
        {
            string p = Path.Combine(DirectoryPath, "changes.json");
            if (!File.Exists(p))
                return new Dictionary<string, Change>();
            try
            {
                var records = json.Deserialize<Dictionary<string, Change>>(File.ReadAllText(p));
                if (records == null || records.Count > 1024)
                    throw new InvalidDataException("Invalid recovery record");
                foreach (var record in records)
                    if (record.Value == null || !System.Text.RegularExpressions.Regex.IsMatch(record.Key, "\\A[0-9a-fA-F]{64}\\z"))
                        throw new InvalidDataException("Invalid recovery identity");
                return records;
            }
            catch
            {
                InvalidJournal = true;
                return new Dictionary<string, Change>();
            }
        }

        internal void SavePreferences(Preferences p)
        {
            var saved = json.Deserialize<Preferences>(json.Serialize(p));
            if (!p.RememberConfiguration)
            {
                saved.ProtectionEnabled = false;
                saved.SelectionMode = "Auto";
                saved.SelectedMicrophone = null;
                saved.LastValidated = null;
                saved.ProcessingSystem = "Automatic";
                saved.EchoSource = "Automatic";
            }

            Write("settings.json", saved);
        }

        internal void SaveJournal(Dictionary<string, Change> journal)
        {
            Write("changes.json", journal);
        }

        void Write(string name, object value)
        {
            string target = Path.Combine(DirectoryPath, name), temp = target + ".new";
            File.WriteAllText(temp, json.Serialize(value));
            if (File.Exists(target))
                File.Replace(temp, target, null);
            else
                File.Move(temp, target);
        }

        internal void Log(string category)
        {
            try
            {
                string p = Path.Combine(DirectoryPath, "events.log");
                if (File.Exists(p) && new FileInfo(p).Length > 131072)
                {
                    File.Copy(p, p + ".previous", true);
                    File.WriteAllText(p, "");
                }

                File.AppendAllText(p, DateTimeOffset.Now.ToString("o") + " " + category + Environment.NewLine);
            }
            catch
            {
            }
        }

        internal static bool StartupEnabled
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    string value = k == null ? null : k.GetValue("EchoGuard") as string;
                    return value != null && value == StartupValue;
                }
            }
        }

        static string StartupValue
        {
            get
            {
                return "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --tray";
            }
        }

        internal static void SetStartup(bool enabled)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (enabled)
                    k.SetValue("EchoGuard", StartupValue);
                else if (Convert.ToString(k.GetValue("EchoGuard")) == StartupValue)
                    k.DeleteValue("EchoGuard", false);
            }
        }
    }
}
