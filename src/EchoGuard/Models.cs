using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoGuard
{
    public sealed class EndpointInfo
    {
        public string Id = "", Name = "", Driver = "", Role = "", Hardware = "";
        public int Flow;
        public uint State = 1;
        public string Container = "";
        public bool DefaultConsole, DefaultMultimedia, DefaultCommunications;
        public string StateLabel
        {
            get
            {
                return State == 1 ? "Available" : State == 2 ? "Disabled" : State == 4 ? "Not present" : State == 8 ? "Disconnected" : "Unknown state";
            }
        }

        public string Classification
        {
            get
            {
                return IsSonar ? "Virtual — Sonar driver" : Hardware.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ? "USB-backed microphone" : "Unknown — not inferred from its name";
            }
        }

        public string DisplayLabel
        {
            get
            {
                return Name + " · " + StateLabel + (DefaultMultimedia ? " · Windows Default Input" : "") + (DefaultCommunications ? " · Communications Default" : "") + " [" + Identity.Hash(Id).Substring(0, 8) + "]";
            }
        }

        public EndpointInfo Copy()
        {
            var e = (EndpointInfo)MemberwiseClone();
            e.Sessions = new List<SessionInfo>(Sessions);
            return e;
        }

        public List<SessionInfo> Sessions = new List<SessionInfo>();
        public bool IsSonar
        {
            get
            {
                return Driver == "ROOT\\VEN_SSGG&DEV_0001";
            }
        }

        public bool IsMic
        {
            get
            {
                return IsSonar && Role == "ChatCapture";
            }
        }
    }

    public sealed class SessionInfo
    {
        public string Key = "";
        public uint Pid;
        public int State;
        public bool Mute, Discord, Sonar, SingleProcess;
    }

    public sealed class Diagnosis
    {
        public string Status = "Checking audio", Detail = "Reading audio-session metadata.", RenderName = "Not detected", CaptureName = "Not detected";
        public bool Compatible, CanEnable;
        public List<string> Candidates = new List<string>();
        public int EndpointCount, DiscordCount;
        public bool Conflict;
        public List<EndpointInfo> Microphones = new List<EndpointInfo>();
        public SelectionResult Selection = new SelectionResult();
        public DeviceIdentity SavedMicrophone;
        public string Processing = "Unknown", DuplicatePath = "Not identified", Compatibility = "Not confirmed", TargetRenderId = "";
        public bool ProtectionEnabled, RememberConfiguration = true, ManualMode;
        public string Summary()
        {
            return "EchoGuard 1.0.0\r\nStatus: " + Status + "\r\n" + Detail + "\r\nActive endpoints: " + EndpointCount + "\r\nVerified Discord processes: " + DiscordCount + "\r\nEligible sessions: " + Candidates.Count + "\r\nOther audio helper: " + Conflict + "\r\nInput confidence: " + Selection.Confidence + "\r\nResolution: " + Selection.Resolution + "\r\nCompatibility: " + Compatibility + "\r\nRecording endpoints: " + Microphones.Count + "\r\nNo audio, device identifiers, process paths or account data included.\r\n";
        }
    }

    public static class Rules
    {
        public static Diagnosis Diagnose(List<EndpointInfo> endpoints, int discordCount, bool conflict)
        {
            var d = new Diagnosis
            {
                EndpointCount = endpoints.Count,
                DiscordCount = discordCount,
                Conflict = conflict
            };
            if (conflict)
            {
                d.Status = "Fix Requires Attention";
                d.Detail = "Another AudioInitialization helper is running. EchoGuard will not change audio while that helper is active.";
                return d;
            }

            var renders = endpoints.Where(e => e.Flow == 0 && e.IsMic).ToList();
            var captures = endpoints.Where(e => e.Flow == 1 && e.IsMic).ToList();
            if (!endpoints.Any(e => e.IsSonar))
            {
                d.Status = "Sonar Not Detected";
                d.Detail = "This preview supports the known SteelSeries Sonar microphone path. Other echo causes need a different diagnosis.";
                return d;
            }

            if (renders.Count != 1 || captures.Count != 1 || renders[0].Hardware != captures[0].Hardware || String.IsNullOrEmpty(renders[0].Hardware))
            {
                d.Status = "Unsupported Audio Setup";
                d.Detail = "A unique, paired Sonar microphone playback and recording endpoint could not be identified. No audio settings will be changed.";
                return d;
            }

            d.Compatible = true;
            d.CanEnable = true;
            d.RenderName = renders[0].Name;
            d.CaptureName = captures[0].Name;
            if (discordCount == 0)
            {
                d.Status = "Discord Not Running";
                d.Detail = "Open the official Discord desktop client, choose Sonar Microphone for voice, then start Entire Screen sharing with audio.";
                return d;
            }

            var voice = captures[0].Sessions.Where(s => s.Discord && s.SingleProcess && s.State == 1 && !s.Mute).Select(s => s.Pid).ToList();
            if (voice.Count == 0)
            {
                d.Status = "Waiting for voice input";
                d.Detail = "Join a Discord voice channel using Sonar Microphone. EchoGuard has not applied a fix.";
                return d;
            }

            if (endpoints.Where(e => e.Flow == 1 && e.Id != captures[0].Id).Any(e => e.Sessions.Any(s => s.Discord && s.State == 1)))
            {
                d.Status = "Unsupported Audio Setup";
                d.Detail = "Discord also has another active recording endpoint. Resolve that input routing before enabling this fix.";
                d.CanEnable = false;
                d.Compatible = false;
                return d;
            }

            if (!renders[0].Sessions.Any(s => s.Sonar && s.State == 1))
            {
                d.Status = "Fix Requires Attention";
                d.Detail = "Sonar's microphone playback session is not active or its publisher could not be verified. No changes will be made.";
                return d;
            }

            d.Candidates = renders[0].Sessions.Where(s => s.Discord && s.SingleProcess && s.State == 1 && voice.Contains(s.Pid)).Select(s => s.Key).ToList();
            d.Status = d.Candidates.Count == 0 ? "Waiting for screen share" : "Compatible session detected";
            d.Detail = d.Candidates.Count == 0 ? "Start Discord Entire Screen sharing with audio. Protection will apply only to a matching microphone capture path." : "Discord is using Sonar for voice and also has a session on Sonar's internal microphone playback endpoint. Enable protection to exclude that extra path.";
            return d;
        }
    }
}
