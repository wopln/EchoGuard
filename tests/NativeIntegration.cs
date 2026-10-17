using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using EchoGuard;

internal static class Integration
{
    static readonly Guid TestContext = new Guid("BDBF33D8-833E-4D3A-95AF-C731FA4071D5");
    static void Wait(AudioEngine e, string expected)
    {
        using (var changed = new AutoResetEvent(false))
        {
            Action<Diagnosis> h = delegate
            {
                changed.Set();
            };
            e.Changed += h;
            try
            {
                DateTime end = DateTime.UtcNow.AddSeconds(10);
                while (e.Current.Status != expected)
                {
                    int ms = (int)(end - DateTime.UtcNow).TotalMilliseconds;
                    if (ms <= 0 || !changed.WaitOne(ms))
                        throw new Exception("Expected " + expected + ", observed " + e.Current.Status);
                }
            }
            finally
            {
                e.Changed -= h;
            }
        }
    }

    static Native.Control Find(string key)
    {
        Native.Enumerator e = Native.CreateEnumerator();
        Native.Collection list;
        Native.Check(e.EnumAudioEndpoints(0, 1, out list));
        try
        {
            uint n;
            Native.Check(list.GetCount(out n));
            for (uint i = 0; i < n; i++)
            {
                Native.Device d;
                Native.Check(list.Item(i, out d));
                try
                {
                    if (Native.Property(d, "a8b865dd-2e3d-4094-ad97-e593a70c75d6", 8) != "ROOT\\VEN_SSGG&DEV_0001" || Native.Property(d, "f141e7cc-921b-466d-802f-879d6d4aec17", 1) != "ChatCapture")
                        continue;
                    Guid iid = Native.SessionManagerId;
                    object o;
                    Native.Check(d.Activate(ref iid, 23, IntPtr.Zero, out o));
                    var m = (Native.Manager)o;
                    Native.SessionList sessions;
                    Native.Check(m.GetSessionEnumerator(out sessions));
                    try
                    {
                        int count;
                        Native.Check(sessions.GetCount(out count));
                        for (int j = 0; j < count; j++)
                        {
                            Native.Control c;
                            Native.Check(sessions.GetSession(j, out c));
                            string id;
                            Native.Check(((Native.Control2)c).GetSessionInstanceIdentifier(out id));
                            if (id == key)
                                return c;
                            Native.Release(c);
                        }
                    }
                    finally
                    {
                        Native.Release(sessions);
                        Native.Release(m);
                    }
                }
                finally
                {
                    Native.Release(d);
                }
            }
        }
        finally
        {
            Native.Release(list);
            Native.Release(e);
        }

        throw new Exception("Matching session disappeared");
    }

    static List<string> Defaults()
    {
        var ids = new List<string>();
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
                        ids.Add(id);
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

        return ids;
    }

    public static int Main(string[] args)
    {
        var steps = new List<string>();
        Native.Control target = null;
        bool original = false;
        AudioEngine engine = null;
        int com = Native.CoInitializeEx(IntPtr.Zero, 0);
        try
        {
            Native.Check(com);
            if (Identity.OtherHelper())
                throw new Exception("Existing helper must be stopped for independent test");
            var before = Defaults();
            string state = Path.Combine(Path.GetTempPath(), "EchoGuard-test-" + Guid.NewGuid().ToString("N"));
            engine = new AudioEngine(state, false, false);
            if (!engine.Ready.WaitOne(15000))
                throw new Exception("Initial scan timed out");
            if (engine.Current.Candidates.Count != 1)
                throw new Exception("Known live path unavailable: " + engine.Current.Status);
            target = Find(engine.Current.Candidates[0]);
            var volume = (Native.Volume)target;
            Native.Check(volume.GetMute(out original));
            Guid context = TestContext;
            Native.Check(volume.SetMute(false, ref context));
            if (args.Length > 1 && args[1] == "prepare-live")
            {
                File.WriteAllText(args[0], "Live test baseline unmuted; restart original helper after testing.");
                original = false;
                return 0;
            }

            engine.SetEnabled(true);
            Wait(engine, "Protected");
            bool mute;
            Native.Check(volume.GetMute(out mute));
            if (!mute)
                throw new Exception("Enable readback failed");
            steps.Add("Enable protection applied and read back native target mute");
            engine.SetEnabled(false);
            Wait(engine, "Ready to Apply Fix");
            Native.Check(volume.GetMute(out mute));
            if (mute)
                throw new Exception("Disable did not restore original false baseline");
            steps.Add("Disable restored exact unmuted baseline");
            engine.SetEnabled(true);
            Wait(engine, "Protected");
            Native.Check(volume.SetMute(false, ref context));
            Wait(engine, "Fix Requires Attention");
            Thread.Sleep(1500);
            Native.Check(volume.GetMute(out mute));
            if (mute)
                throw new Exception("External override was fought");
            steps.Add("External mute override respected; attention reported without fighting");
            engine.SetEnabled(false);
            Wait(engine, "Ready to Apply Fix");
            engine.SetEnabled(true);
            Wait(engine, "Protected");
            engine.Dispose();
            engine = null;
            Native.Check(volume.GetMute(out mute));
            if (mute)
                throw new Exception("Exit restoration failed");
            steps.Add("Exit restored owned change");
            if (!before.SequenceEqual(Defaults()))
                throw new Exception("Windows default devices changed");
            steps.Add("All six Windows audio defaults unchanged");
            File.WriteAllText(args[0], new JavaScriptSerializer().Serialize(new { Passed = true, Steps = steps, AudioListeningTest = "Not performed by this native metadata test", UserDataIncluded = false }));
            Console.WriteLine("PASS " + steps.Count + " native integration checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL " + ex.Message);
            return 1;
        }
        finally
        {
            if (engine != null)
                engine.Dispose();
            if (target != null)
            {
                Guid c = TestContext;
                ((Native.Volume)target).SetMute(original, ref c);
                Native.Release(target);
            }

            if (com >= 0)
                Native.CoUninitialize();
        }
    }
}
