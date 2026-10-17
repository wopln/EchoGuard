using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using EchoGuard;

internal static class NativeDetectionTests
{
    static int count;
    static void Check(bool value, string label)
    {
        if (!value)
            throw new Exception(label);
        count++;
        Console.WriteLine("PASS " + label);
    }

    static List<string> Defaults()
    {
        var result = new List<string>();
        var e = Native.CreateEnumerator();
        try
        {
            for (int f = 0; f < 2; f++)
                for (int r = 0; r < 3; r++)
                {
                    Native.Device d;
                    Native.Check(e.GetDefaultAudioEndpoint(f, r, out d));
                    try
                    {
                        string id;
                        Native.Check(d.GetId(out id));
                        result.Add(id);
                    }
                    finally
                    {
                        Native.Release(d);
                    }
                }
        }
        finally
        {
            Native.Release(e);
        }

        return result;
    }

    static void Step(AudioEngine e, Action change, Func<Diagnosis, bool> expected)
    {
        using (var signal = new AutoResetEvent(false))
        {
            Action<Diagnosis> cb = delegate
            {
                signal.Set();
            };
            e.Changed += cb;
            try
            {
                change();
                DateTime end = DateTime.UtcNow.AddSeconds(10);
                do
                {
                    int remaining = (int)(end - DateTime.UtcNow).TotalMilliseconds;
                    if (remaining <= 0 || !signal.WaitOne(remaining))
                        throw new Exception("Native event/configuration update timed out");
                }
                while (!expected(e.Current));
            }
            finally
            {
                e.Changed -= cb;
            }
        }
    }

    public static int Main(string[] args)
    {
        int com = Native.CoInitializeEx(IntPtr.Zero, 0);
        string state = Path.Combine(Path.GetTempPath(), "EchoGuard-detection-" + Guid.NewGuid().ToString("N"));
        try
        {
            Native.Check(com);
            if (!Identity.OtherHelper())
                throw new Exception("This read/configuration test expects the original helper to remain active; it never stops it.");
            var defaults = Defaults();
            List<EndpointInfo> inventory;
            var enumerator = Native.CreateEnumerator();
            try
            {
                inventory = AudioInventory.Read(enumerator);
            }
            finally
            {
                Native.Release(enumerator);
            }

            var inputs = inventory.Where(e => e.Flow == 1).ToList();
            Check(inputs.Count > 1 && inputs.Select(e => e.Id).Distinct().Count() == inputs.Count, "real multiple recording endpoints enumerated by unique IDs");
            Check(inputs.Any(e => e.State != 1), "real inactive recording endpoints and their states enumerated");
            Check(inputs.Count(e => e.DefaultMultimedia) == 1 && inputs.Count(e => e.DefaultCommunications) == 1, "real Windows input role assignments resolved");
            using (var e = new AudioEngine(state, false, false))
            {
                Check(e.Ready.WaitOne(15000), "event-driven engine initialized");
                Check(e.Current.Microphones.Count == inputs.Count, "engine exposes full recording inventory including inactive devices");
                Check(e.Current.Conflict && !e.Current.CanEnable && e.Current.Candidates.Count == 0, "original helper conflict blocks exclusion while allowing discovery");
                var selected = inputs.First(m => m.State == 1 && !m.IsMic);
                Step(e, delegate
                {
                    e.SelectMicrophone(selected.Id);
                }, d => d.ManualMode && d.SavedMicrophone != null && d.SavedMicrophone.EndpointId == selected.Id);
                Check(e.Current.Selection.Microphone != null && e.Current.Selection.Microphone.Id == selected.Id && !e.Current.ProtectionEnabled, "real manual selection stays selected and disables protection");
                Check(new LocalStore(state).LoadPreferences().SelectedMicrophone.EndpointId == selected.Id, "real manual identity persisted locally");
                Step(e, delegate
                {
                    e.SetEnabled(true);
                }, d => d.Conflict && !d.ProtectionEnabled);
                Check(e.PendingChanges == 0, "conflicting / unsupported configuration cannot create owned audio changes");
                Step(e, delegate
                {
                    e.RememberConfiguration(false);
                }, d => !d.RememberConfiguration);
                var saved = new LocalStore(state).LoadPreferences();
                Check(saved.SelectedMicrophone == null && !saved.ProtectionEnabled && saved.SelectionMode == "Auto", "remember off removes persisted microphone identity and startup protection consent");
                Step(e, delegate
                {
                    e.SelectMicrophone(null);
                }, d => !d.ManualMode);
                Check(e.Current.Selection.Microphone != null || e.Current.Selection.Confidence == "Unknown", "automatic mode returns an explicit evidence-based result");
            }

            using (var e = new AudioEngine(state, false, false))
            {
                Check(e.Ready.WaitOne(15000) && !e.Current.ManualMode && !e.Current.ProtectionEnabled, "new engine instance respects forgotten configuration");
            }

            Check(defaults.SequenceEqual(Defaults()), "all six Windows default roles unchanged throughout native tests");
            File.WriteAllText(args[0], new JavaScriptSerializer().Serialize(new { Passed = true, Checks = count, RecordingEndpoints = inputs.Count, Available = inputs.Count(m => m.State == 1), ActualHelperPreserved = true, AudioSamplesCaptured = false, LiveSoundVerification = "Pending for 0.2; no protector overlap used" }));
            Console.WriteLine("Passed " + count + " native detection/configuration checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL " + ex.Message);
            return 1;
        }
        finally
        {
            if (com >= 0)
                Native.CoUninitialize();
        }
    }
}
