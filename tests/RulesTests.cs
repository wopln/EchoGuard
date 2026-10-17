using System;
using System.Collections.Generic;
using EchoGuard;

internal static class Tests
{
    static int passed;
    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        passed++;
        Console.WriteLine("PASS " + name);
    }

    static List<EndpointInfo> Good()
    {
        var r = new EndpointInfo
        {
            Id = "render",
            Flow = 0,
            Name = "Sonar microphone playback",
            Driver = "ROOT\\VEN_SSGG&DEV_0001",
            Role = "ChatCapture",
            Hardware = "synthetic-root"
        };
        r.Sessions.Add(new SessionInfo { Key = "writer", Pid = 10, State = 1, Sonar = true });
        r.Sessions.Add(new SessionInfo { Key = "loopback", Pid = 20, State = 1, Discord = true, SingleProcess = true });
        var c = new EndpointInfo
        {
            Id = "capture",
            Flow = 1,
            Name = "Sonar microphone",
            Driver = r.Driver,
            Role = r.Role,
            Hardware = r.Hardware
        };
        c.Sessions.Add(new SessionInfo { Key = "voice", Pid = 20, State = 1, Discord = true, SingleProcess = true });
        return new List<EndpointInfo>
        {
            r,
            c
        };
    }

    public static int Main()
    {
        try
        {
            var e = Good();
            var d = Rules.Diagnose(e, 1, false);
            Check(d.Compatible && d.CanEnable && d.Candidates.Count == 1, "known paired active path is eligible");
            Check(d.Status != "Protected", "diagnosis alone never reports protected");
            Check(Rules.Diagnose(e, 1, true).Candidates.Count == 0 && !Rules.Diagnose(e, 1, true).CanEnable, "existing helper blocks all candidates");
            Check(Rules.Diagnose(new List<EndpointInfo>(), 1, false).Status == "Sonar Not Detected", "absence of Sonar is explicit");
            e = Good();
            e[0].Driver = "unrelated-driver";
            Check(!Rules.Diagnose(e, 1, false).Compatible, "friendly name alone cannot identify endpoint");
            e = Good();
            e.Add(e[0]);
            Check(!Rules.Diagnose(e, 1, false).Compatible, "ambiguous render endpoints rejected");
            e = Good();
            e.Add(e[1]);
            Check(!Rules.Diagnose(e, 1, false).Compatible, "ambiguous capture endpoints rejected");
            e = Good();
            e[1].Hardware = "different-root";
            Check(!Rules.Diagnose(e, 1, false).Compatible, "unpaired root identities rejected");
            e = Good();
            e[0].Hardware = "";
            e[1].Hardware = "";
            Check(!Rules.Diagnose(e, 1, false).Compatible, "missing root identities rejected");
            Check(Rules.Diagnose(Good(), 0, false).Status == "Discord Not Running", "absence of verified Discord is explicit");
            e = Good();
            e[1].Sessions[0].State = 0;
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 0, "inactive voice does not authorize muting");
            e = Good();
            e[1].Sessions[0].Mute = true;
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 0, "muted recording session is not treated as healthy voice path");
            e = Good();
            e[0].Sessions[0].Sonar = false;
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 0, "unverified Sonar writer rejected");
            e = Good();
            e[0].Sessions[1].Discord = false;
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 0, "unverified Discord session rejected");
            e = Good();
            e[0].Sessions[1].Pid = 30;
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 0, "different Discord process voice/capture rejected");
            e = Good();
            e[0].Sessions[1].SingleProcess = false;
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 0, "cross-process target rejected");
            e = Good();
            e[0].Sessions[1].State = 0;
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 0, "inactive target waits for activation");
            e = Good();
            var other = new EndpointInfo
            {
                Id = "other-input",
                Flow = 1
            };
            other.Sessions.Add(new SessionInfo { State = 1, Discord = true });
            e.Add(other);
            Check(!Rules.Diagnose(e, 1, false).CanEnable, "multiple Discord recording routes rejected");
            e = Good();
            var game = new EndpointInfo
            {
                Id = "game-output",
                Flow = 0
            };
            game.Sessions.Add(new SessionInfo { Key = "game", State = 1, Pid = 88 });
            e.Add(game);
            Check(Rules.Diagnose(e, 1, false).Candidates.Count == 1, "unrelated game playback is not a candidate");
            e = Good();
            e[0].Sessions.Add(new SessionInfo { Key = "other-app", Pid = 90, State = 1 });
            Check(!Rules.Diagnose(e, 1, false).Candidates.Contains("other-app"), "other applications never targeted");
            var identity = new Identity();
            Check(!identity.Verify((uint)System.Diagnostics.Process.GetCurrentProcess().Id, false), "unsigned test process cannot impersonate official Discord");
            Check(Identity.Hash("session-1") == Identity.Hash("session-1") && Identity.Hash("session-1") != Identity.Hash("session-2"), "journal identities are stable and session-specific hashes");
            Check(!Rules.Diagnose(Good(), 1, false).Summary().Contains("synthetic-root"), "diagnostic export omits hardware identities");
            Check(ChangePolicy.ShouldRestore(new Change { OriginalMute = false }, true), "owned mute is restored to unmuted baseline");
            Check(!ChangePolicy.ShouldRestore(new Change { OriginalMute = true }, true), "pre-existing mute is preserved");
            Check(!ChangePolicy.ShouldRestore(new Change { OriginalMute = false }, false), "already restored mute is left alone");
            Check(!ChangePolicy.ShouldRestore(new Change { OriginalMute = false, ExternalOverride = true }, true), "external override is preserved during restoration");
            Check(!ChangePolicy.NotificationMatchesCurrent(false, true), "stale baseline callback cannot surrender restoration ownership");
            Check(ChangePolicy.NotificationMatchesCurrent(false, false), "current external mute change remains observable");
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EchoGuard-rules-" + Guid.NewGuid().ToString("N"));
            var local = new LocalStore(temp);
            Check(!local.LoadPreferences().ProtectionEnabled, "new user protection is opt-in");
            Check(local.LoadJournal().Count == 0 && !local.InvalidJournal, "absent recovery journal is safe");
            var records = new Dictionary<string, Change>();
            records[Identity.Hash("synthetic-session")] = new Change
            {
                OriginalMute = false
            };
            local.SaveJournal(records);
            Check(local.LoadJournal().Count == 1 && !local.InvalidJournal, "atomic recovery journal round-trips");
            foreach (string corrupt in new[]
            {
                "invalid json",
                "null",
                "{\"bad-key\":{}}",
                "{\"" + Identity.Hash("synthetic-session") + "\":null}"
            }

            )
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(temp, "changes.json"), corrupt);
                var damaged = new LocalStore(temp);
                Check(damaged.LoadJournal().Count == 0 && damaged.InvalidJournal, "damaged recovery journal fails closed: " + corrupt.Substring(0, Math.Min(10, corrupt.Length)));
            }

            // Only these known test files are removed; never recursively remove a supplied directory.
            System.IO.File.Delete(System.IO.Path.Combine(temp, "changes.json"));
            System.IO.Directory.Delete(temp);
            SmartSelectionTests.Run(Check);UiPresentationTests.Run(Check);
            Console.WriteLine("Passed " + passed + " tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL " + ex.Message);
            return 1;
        }
    }
}

