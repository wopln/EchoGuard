using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoGuard
{
    public sealed class DeviceIdentity
    {
        public string EndpointId = "", Name = "", Hardware = "", Container = "", Driver = "", Role = "";
        public static DeviceIdentity From(EndpointInfo e)
        {
            return new DeviceIdentity
            {
                EndpointId = e.Id,
                Name = e.Name,
                Hardware = e.Hardware,
                Container = e.Container,
                Driver = e.Driver,
                Role = e.Role
            };
        }
    }

    public sealed class ValidatedConfiguration
    {
        public DeviceIdentity Microphone, Target;
        public string ProcessingSystem = "SteelSeries Sonar", ValidatedUtc = "";
        public int SessionCount;
    }

    public sealed class SelectionResult
    {
        public EndpointInfo Microphone;
        public string Confidence = "Unknown", Resolution = "Unavailable", Explanation = "No recording endpoint identified.";
    }

    public static class MicrophoneSelection
    {
        public static bool Voice(EndpointInfo e)
        {
            return e.Flow == 1 && e.State == 1 && e.Sessions.Any(s => s.Discord && s.SingleProcess && s.State == 1 && !s.Mute);
        }

        public static SelectionResult Resolve(List<EndpointInfo> endpoints, Preferences p)
        {
            if (p.SelectionMode != "Auto" && p.SelectionMode != "Manual")
                return new SelectionResult
                {
                    Explanation = "Saved selection mode is not supported. Select Auto Detect or a microphone explicitly."
                };
            var inputs = endpoints.Where(e => e.Flow == 1).ToList();
            var live = inputs.Where(e => e.State == 1).ToList();
            var voice = live.Where(Voice).ToList();
            if (p.SelectionMode == "Manual")
            {
                var saved = p.SelectedMicrophone;
                if (saved == null || String.IsNullOrEmpty(saved.EndpointId))
                    return new SelectionResult
                    {
                        Explanation = "Select a microphone. The saved manual selection is missing."
                    };
                var exact = inputs.Where(e => e.Id == saved.EndpointId).ToList();
                if (exact.Count == 1 && exact[0].State == 1)
                {
                    var e = exact[0];
                    if ((!String.IsNullOrEmpty(saved.Hardware) && e.Hardware != saved.Hardware) || (!String.IsNullOrEmpty(saved.Driver) && e.Driver != saved.Driver) || (!String.IsNullOrEmpty(saved.Role) && e.Role != saved.Role) || (!String.IsNullOrEmpty(saved.Container) && e.Container != saved.Container))
                        return new SelectionResult
                        {
                            Microphone = e,
                            Resolution = "Identity changed",
                            Explanation = "The saved endpoint ID now has different hardware or endpoint properties. Choose Auto Detect, then reselect the intended microphone after reviewing its identity."
                        };
                    return Manual(e, voice, "Exact endpoint identity");
                }

                if (exact.Count == 1 && exact[0].State == 2)
                    return new SelectionResult
                    {
                        Microphone = exact[0],
                        Explanation = "Your selected microphone is disabled. EchoGuard will not enable it or choose another device."
                    };
                // Hardware identity alone can describe several pins. Require a nonempty endpoint role,
                // driver and PnP identity; include the saved container when it was available. Never use names.
                var matches = live.Where(e => !String.IsNullOrEmpty(saved.Hardware) && !String.IsNullOrEmpty(saved.Role) && !String.IsNullOrEmpty(saved.Driver) && e.Hardware == saved.Hardware && e.Role == saved.Role && e.Driver == saved.Driver && (String.IsNullOrEmpty(saved.Container) || e.Container == saved.Container)).ToList();
                if (matches.Count == 1)
                    return Manual(matches[0], voice, "Unique hardware, driver and endpoint-role match");
                return new SelectionResult
                {
                    Microphone = exact.Count == 1 ? exact[0] : null,
                    Resolution = matches.Count > 1 ? "Ambiguous" : "Unavailable",
                    Explanation = matches.Count > 1 ? "Multiple endpoints match your saved microphone. Select the intended device; no exclusion will be applied." : "Your selected microphone is unavailable. Reconnect it or select a microphone. A device name alone is never used to replace it."
                };
            }

            if (voice.Count == 1)
                return new SelectionResult
                {
                    Microphone = voice[0],
                    Confidence = "Confirmed",
                    Resolution = "Active Discord recording session",
                    Explanation = "A verified Discord process has one active, unmuted recording session on this endpoint. This confirms session routing, not the client's private setting or audible sound."
                };
            if (voice.Count > 1)
                return new SelectionResult
                {
                    Resolution = "Ambiguous",
                    Explanation = "Discord has multiple active recording endpoints. EchoGuard cannot identify a unique voice input safely."
                };
            var suggestion = live.Where(e => e.DefaultCommunications).ToList();
            if (suggestion.Count != 1)
                suggestion = live.Where(e => e.DefaultMultimedia).ToList();
            if (suggestion.Count != 1)
                suggestion = live.Where(e => e.DefaultConsole).ToList();
            if (suggestion.Count != 1 && live.Count == 1)
                suggestion = live;
            return suggestion.Count == 1 ? new SelectionResult
            {
                Microphone = suggestion[0],
                Confidence = "Suggested",
                Resolution = "Windows preference / available endpoint",
                Explanation = "This is an estimate from Windows input preferences or the only available microphone. Discord's active input has not been confirmed."
            }

            : new SelectionResult
            {
                Explanation = "No unique active Discord input or Windows preference is available. Join voice or select your microphone."
            };
        }

        static SelectionResult Manual(EndpointInfo e, List<EndpointInfo> voice, string resolution)
        {
            return new SelectionResult
            {
                Microphone = e,
                Resolution = resolution,
                Confidence = voice.Count == 1 && voice[0].Id == e.Id ? "Confirmed" : "Unknown",
                Explanation = voice.Count == 1 && voice[0].Id != e.Id ? "The selected microphone does not match Discord's observed active recording endpoint. EchoGuard will not switch your selection or change Discord." : voice.Count > 1 ? "Multiple Discord recording endpoints are active; voice routing is ambiguous." : voice.Count == 0 ? "Manual selection is identified, but no active Discord voice input is confirmed on it." : "Your selected endpoint matches the verified active Discord recording session."
            };
        }

        public static Diagnosis Diagnose(List<EndpointInfo> endpoints, Preferences prefs, Diagnosis d)
        {
            var r = Resolve(endpoints, prefs);
            if (r.Microphone != null)
                r.Microphone = r.Microphone.Copy();
            d.Selection = r;
            d.Microphones = endpoints.Where(e => e.Flow == 1).OrderBy(e => e.State == 1 ? 0 : 1).ThenBy(e => e.DefaultCommunications ? 0 : 1).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).Select(e => e.Copy()).ToList();
            d.ProtectionEnabled = prefs.ProtectionEnabled;
            d.RememberConfiguration = prefs.RememberConfiguration;
            d.ManualMode = prefs.SelectionMode == "Manual";
            d.SavedMicrophone = prefs.SelectedMicrophone;
            d.Processing = r.Microphone != null && r.Microphone.IsMic ? "SteelSeries Sonar (driver identified)" : "Unknown — no supported processing relationship identified";
            d.DuplicatePath = "Not identified";
            d.Compatibility = "Not confirmed";
            var mic = r.Microphone;
            if (mic == null || mic.State != 1 || r.Confidence != "Confirmed" || !mic.IsMic || (prefs.ProcessingSystem != "Automatic" && prefs.ProcessingSystem != "SteelSeries Sonar") || prefs.EchoSource != "Automatic")
            {
                d.Compatible = false;
                d.CanEnable = false;
                d.Candidates.Clear();
                if (mic != null && !mic.IsMic)
                    d.Compatibility = "Unsupported by this preview";
                if (!d.Conflict)
                {
                    d.Status = mic != null && !mic.IsMic ? "Unsupported Audio Setup" : "Ready to Diagnose";
                    d.Detail = r.Explanation + (mic != null && !mic.IsMic ? " This preview protects the known Sonar/Discord path only. A physical microphone's upstream relationship to Sonar cannot be inferred safely." : "");
                }

                return d;
            }

            if (!d.CanEnable || d.Conflict || d.Candidates.Count == 0)
            {
                d.CanEnable = false;
                return d;
            }

            var targets = endpoints.Where(e => e.State == 1 && e.Flow == 0 && e.IsMic && e.Hardware == mic.Hardware).ToList();
            if (targets.Count != 1)
            {
                d.Compatible = false;
                d.CanEnable = false;
                d.Candidates.Clear();
                return d;
            }

            d.TargetRenderId = targets[0].Id;
            d.DuplicatePath = "Discord → " + targets[0].Name + " (microphone playback session)";
            d.Compatibility = "Supported known path";
            d.Status = "Ready to Apply Fix";
            d.Detail = "The selected Sonar voice input and matching extra Discord microphone playback path are identified. This is evidence of a supported routing scenario, not proof of audible echo.";
            return d;
        }
    }
}
