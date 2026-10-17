using System;
using System.Collections.Generic;
using System.IO;
using EchoGuard;

internal static class SmartSelectionTests
{
    static EndpointInfo Mic(string id, string hardware, bool voice)
    {
        var e = new EndpointInfo
        {
            Id = id,
            Name = "Same microphone name",
            Flow = 1,
            Hardware = hardware,
            Container = "test-container-" + hardware,
            Driver = "test-driver",
            Role = "test-pin"
        };
        if (voice)
            e.Sessions.Add(new SessionInfo { Discord = true, SingleProcess = true, State = 1, Pid = 42 });
        return e;
    }

    static List<EndpointInfo> Known()
    {
        var m = Mic("sonar-input", "sonar-root", true);
        m.Driver = "ROOT\\VEN_SSGG&DEV_0001";
        m.Role = "ChatCapture";
        var r = new EndpointInfo
        {
            Id = "sonar-playback",
            Name = "Internal microphone playback",
            Flow = 0,
            Hardware = m.Hardware,
            Driver = m.Driver,
            Role = m.Role
        };
        r.Sessions.Add(new SessionInfo { Sonar = true, State = 1, Pid = 10 });
        r.Sessions.Add(new SessionInfo { Key = "discord-share", Discord = true, SingleProcess = true, State = 1, Pid = 42 });
        return new List<EndpointInfo>
        {
            m,
            r
        };
    }

    static Diagnosis Diagnose(List<EndpointInfo> all, Preferences p)
    {
        return MicrophoneSelection.Diagnose(all, p, Rules.Diagnose(all.FindAll(e => e.State == 1), 1, false));
    }

    internal static void Run(Action<bool, string> Check)
    {
        var auto = new Preferences();
        var all = Known();
        all.Add(Mic("physical", "USB\\SYNTHETIC", false));
        all[2].DefaultCommunications = true;
        Check(Diagnose(all, auto).Selection.Microphone.Id == "sonar-input", "auto prefers confirmed Discord capture over different Windows default");
        Check(Diagnose(all, auto).Selection.Confidence == "Confirmed", "active verified voice evidence is labeled confirmed");
        Check(Diagnose(all, auto).Microphones.Count == 2, "multiple recording devices enumerated separately from playback");
        Check(Diagnose(all, auto).CanEnable && Diagnose(all, auto).TargetRenderId == "sonar-playback", "original supported exclusion still targets render, never input");
        Check(Diagnose(all, auto).Status != "Protected", "selection diagnosis never claims an applied exclusion");
        all[0].Sessions.Clear();
        var suggested = Diagnose(all, auto);
        Check(suggested.Selection.Microphone.Id == "physical" && suggested.Selection.Confidence == "Suggested", "Windows communications preference is only a suggestion");
        Check(!suggested.CanEnable && suggested.Candidates.Count == 0, "suggested microphone cannot authorize an exclusion");
        all = Known();
        var manual = new Preferences
        {
            SelectionMode = "Manual",
            SelectedMicrophone = DeviceIdentity.From(all[0])
        };
        Check(Diagnose(all, manual).CanEnable, "manual Sonar selection supports original diagnosed path");
        string hardware = all[0].Hardware;
        all[0].Hardware = "replacement-hardware";
        Check(!Diagnose(all, manual).CanEnable && Diagnose(all, manual).Selection.Resolution == "Identity changed", "reused endpoint ID with different hardware cannot authorize protection");
        all[0].Hardware = hardware;
        string label = all[0].Name;
        all[0].Name = "Renamed microphone";
        Check(Diagnose(all, manual).CanEnable, "renaming an identical endpoint does not break selection identity");
        all[0].Name = label;
        var physical = Mic("physical", "USB\\SYNTHETIC", false);
        all.Add(physical);
        manual.SelectedMicrophone = DeviceIdentity.From(physical);
        Check(Diagnose(all, manual).Selection.Microphone.Id == "physical" && !Diagnose(all, manual).CanEnable, "manual physical choice is never silently replaced by Sonar");
        Check(Diagnose(all, manual).Selection.Explanation.Contains("does not match"), "manual mismatch explains observed Discord routing");
        all[0].Sessions.Clear();
        physical.Sessions.Add(new SessionInfo { Discord = true, SingleProcess = true, State = 1, Pid = 42 });
        Check(Diagnose(all, manual).Selection.Confidence == "Confirmed" && !Diagnose(all, manual).CanEnable, "confirmed physical voice is explicitly unsupported by Sonar-specific fix");
        var duplicate = Mic("different-id", "different-hardware", false);
        all.Add(duplicate);
        Check(MicrophoneSelection.Resolve(all, manual).Microphone.Id == "physical", "identical names do not confuse exact endpoint selection");
        Check(physical.DisplayLabel != duplicate.DisplayLabel, "identical names have distinct UI identity suffixes");
        physical.State = 8;
        Check(MicrophoneSelection.Resolve(all, manual).Confidence == "Unknown", "USB unplug does not switch to another named microphone");
        Check(!Diagnose(all, manual).CanEnable, "disconnected input blocks protection");
        physical.State = 1;
        Check(MicrophoneSelection.Resolve(all, manual).Microphone.Id == "physical", "same endpoint USB reconnection restores manual identity");
        physical.State = 2;
        Check(MicrophoneSelection.Resolve(all, manual).Explanation.Contains("disabled"), "disabled microphone is preserved without remapping");
        all = Known();
        manual.SelectedMicrophone = DeviceIdentity.From(all[0]);
        all[0].Id = "recreated-sonar-id";
        Check(MicrophoneSelection.Resolve(all, manual).Microphone.Id == "recreated-sonar-id", "unique hardware driver role rebind survives endpoint ID recreation");
        var twin = all[0].Copy();
        twin.Id = "second-recreated-id";
        all.Add(twin);
        Check(MicrophoneSelection.Resolve(all, manual).Resolution == "Ambiguous", "multiple matching recreated endpoints require user selection");
        all.Remove(twin);
        all[0].Container = "different-container";
        Check(MicrophoneSelection.Resolve(all, manual).Microphone == null, "container identity mismatch prevents unsafe remapping");
        all[0].Container = manual.SelectedMicrophone.Container;
        all[0].Role = "different-pin";
        Check(MicrophoneSelection.Resolve(all, manual).Microphone == null, "different endpoint role cannot replace selected input");
        all[0].Role = manual.SelectedMicrophone.Role;
        manual.SelectedMicrophone.Role = "";
        Check(MicrophoneSelection.Resolve(all, manual).Microphone == null, "hardware without endpoint-role evidence cannot rebind");
        all = Known();
        all.Add(Mic("broadcast", "synthetic-virtual-driver", false));
        all.Add(Mic("mixer", "synthetic-mixer", false));
        Check(Diagnose(all, auto).CanEnable, "coexisting inactive virtual inputs do not steal Sonar selection");
        all[2].Sessions.Add(new SessionInfo { Discord = true, SingleProcess = true, State = 1, Pid = 42 });
        Check(MicrophoneSelection.Resolve(all, auto).Resolution == "Ambiguous" && !Diagnose(all, auto).CanEnable, "multiple active virtual recording routes fail closed");
        Check(!Diagnose(Known(), new Preferences { SelectionMode = "FutureMode" }).CanEnable, "unknown saved mode does not fall back to automatic protection");
        Check(!Diagnose(Known(), new Preferences { SelectionMode = "Manual" }).CanEnable, "missing manual identity requires explicit selection");
        Check(!Diagnose(Known(), new Preferences { ProcessingSystem = "Experimental" }).CanEnable, "unsupported processing choice cannot authorize protection");
        Check(!Diagnose(Known(), new Preferences { EchoSource = "Arbitrary device" }).CanEnable, "arbitrary echo source cannot authorize protection");
        all = Known();
        var blocked = MicrophoneSelection.Diagnose(all, auto, Rules.Diagnose(all, 1, true));
        Check(blocked.Conflict && !blocked.CanEnable && blocked.Candidates.Count == 0, "existing helper conflict survives smart selection");
        all = Known();
        all[0].Sessions[0].Discord = false;
        Check(Diagnose(all, auto).Selection.Confidence != "Confirmed", "unverified recording process cannot confirm input");
        all = Known();
        all[0].Sessions.Clear();
        all[0].Sessions.Add(new SessionInfo { Discord = true, SingleProcess = true, State = 1, Pid = 99 });
        Check(!Diagnose(all, auto).CanEnable, "voice and extra playback process mismatch preserves original safety gate");
        all = Known();
        all[1].Sessions.RemoveAt(1);
        Check(!Diagnose(all, auto).CanEnable, "stopped stream waits without claiming protection");
        all[1].Sessions.Add(new SessionInfo { Key = "new-share", Discord = true, SingleProcess = true, State = 1, Pid = 42 });
        Check(Diagnose(all, auto).CanEnable, "recreated streaming session is detected after simulated Discord restart");
        all[0].Sessions[0].Pid = 77;
        all[1].Sessions[1].Pid = 77;
        Check(Diagnose(all, auto).CanEnable, "new Discord process and sharing session recover in the routing model");
        Check(Mic("fake", "unknown", false).Classification.StartsWith("Unknown"), "device class is not inferred from friendly names");
        string dir = Path.Combine(Path.GetTempPath(), "EchoGuard-smart-" + Guid.NewGuid().ToString("N"));
        var store = new LocalStore(dir);
        var prefs = new Preferences
        {
            SelectionMode = "Manual",
            SelectedMicrophone = DeviceIdentity.From(Known()[0]),
            ProtectionEnabled = true,
            StartWithWindows = true,
            LastValidated = new ValidatedConfiguration
            {
                Microphone = DeviceIdentity.From(Known()[0]),
                Target = DeviceIdentity.From(Known()[1]),
                SessionCount = 1
            }
        };
        store.SavePreferences(prefs);
        var loaded = new LocalStore(dir).LoadPreferences();
        Check(loaded.SelectionMode == "Manual" && loaded.SelectedMicrophone.EndpointId == "sonar-input", "manual selection survives new store instance / application restart");
        Check(loaded.ProtectionEnabled && loaded.StartWithWindows && loaded.LastValidated.Target.EndpointId == "sonar-playback", "consent startup and validation record round-trip together");
        loaded.ProtectionEnabled = false;
        store.SavePreferences(loaded);
        loaded = store.LoadPreferences();
        Check(loaded.SelectedMicrophone.EndpointId == "sonar-input", "saving protection toggle preserves microphone configuration");
        loaded.RememberConfiguration = false;
        store.SavePreferences(loaded);
        loaded = store.LoadPreferences();
        Check(!loaded.RememberConfiguration && loaded.SelectionMode == "Auto" && loaded.SelectedMicrophone == null && !loaded.ProtectionEnabled && loaded.LastValidated == null, "remember off forgets device identity and future automatic protection");
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"ProtectionEnabled\":true}");
        loaded = store.LoadPreferences();
        Check(loaded.SelectionMode == "Auto" && loaded.RememberConfiguration, "legacy preview preferences migrate with safe automatic mode");
        File.Delete(Path.Combine(dir, "settings.json"));
        Directory.Delete(dir);
    }
}
