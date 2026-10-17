using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EchoGuard
{
    public sealed class AudioEngine : IDisposable
    {
        readonly LocalStore store;
        readonly Identity identity = new Identity();
        readonly bool readOnly;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        readonly Thread thread;
        readonly HashSet<string> rebindIds = new HashSet<string>();
        readonly Dictionary<string, Watch> watches = new Dictionary<string, Watch>();
        readonly Dictionary<int, Process> processes = new Dictionary<int, Process>();
        Dictionary<string, Change> journal;
        Preferences preferences;
        List<EndpointInfo> inventory = new List<EndpointInfo>();
        bool inventoryComplete;
        Native.Enumerator enumerator;
        DeviceEvents deviceEvents;
        bool enabled, closing, topologyPending, refreshPending;
        readonly object gate = new object ();
        string lastStatus = "";
        public int PendingChanges
        {
            get
            {
                return journal.Count;
            }
        }

        public bool RestorationIncomplete
        {
            get
            {
                return store.InvalidJournal || journal.Count > 0;
            }
        }

        public readonly ManualResetEvent Ready = new ManualResetEvent(false);
        public Diagnosis Current { get; private set; }

        public event Action<Diagnosis> Changed;
        static readonly Guid Context = new Guid("5D428CE7-7A33-4966-B288-5E239F7996CC");
        internal sealed class Watch
        {
            internal EndpointInfo Info;
            internal Native.Manager Manager;
            internal CreatedEvents Created;
            internal bool Active = true;
            internal Dictionary<string, Held> Sessions = new Dictionary<string, Held>();
        }

        internal sealed class Held
        {
            internal Native.Control Control;
            internal SessionEvents Events;
            internal SessionInfo Info;
            internal string JournalKey;
            internal bool LastMute;
            internal bool NeedsApplication = true;
            internal int LifecycleState;
        }

        public AudioEngine(string stateDirectory, bool enabledInitially, bool diagnosticsOnly)
        {
            store = new LocalStore(stateDirectory);
            journal = store.LoadJournal();
            preferences = store.LoadPreferences();
            preferences.ProtectionEnabled = enabledInitially;
            enabled = enabledInitially;
            readOnly = diagnosticsOnly;
            Current = new Diagnosis();
            thread = new Thread(Run)
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        internal void Post(Action action)
        {
            lock (gate)
            {
                if (closing)
                    return;
                queue.Enqueue(action);
                wake.Set();
            }
        }

        public void RunDiagnostics()
        {
            RequestTopology();
        }

        public void SetEnabled(bool value)
        {
            Post(delegate
            {
                if (readOnly)
                    return;
                if (value)
                {
                    var d = Evaluate(false);
                    if (!d.CanEnable || d.Conflict)
                    {
                        Publish(d);
                        return;
                    }
                }

                enabled = value;
                if (value)
                {
                    foreach (var w in watches.Values)
                        foreach (var h in w.Sessions.Values)
                            h.NeedsApplication = true;
                }

                preferences.ProtectionEnabled = value;
                store.SavePreferences(preferences);
                if (!value)
                    RestoreAll();
                Evaluate(true);
            });
        }

        // Selecting a device is diagnostic intent, never permission to mute it or another session.
        public void SelectMicrophone(string endpointId)
        {
            Post(delegate
            {
                if (readOnly)
                    return;
                EndpointInfo selected = endpointId == null ? null : inventory.FirstOrDefault(e => e.Flow == 1 && e.Id == endpointId);
                if (endpointId != null && selected == null)
                {
                    Fail(new InvalidOperationException("Selection is no longer available"));
                    return;
                }

                RestoreAll();
                if (journal.Count > 0)
                {
                    Fail(new InvalidOperationException("Restore before changing selection"));
                    return;
                }

                enabled = false;
                preferences.ProtectionEnabled = false;
                preferences.SelectionMode = endpointId == null ? "Auto" : "Manual";
                preferences.SelectedMicrophone = selected == null ? null : DeviceIdentity.From(selected);
                preferences.LastValidated = null;
                store.SavePreferences(preferences);
                Evaluate(false);
            });
        }

        public void RememberConfiguration(bool value)
        {
            Post(delegate
            {
                if (readOnly)
                    return;
                preferences.RememberConfiguration = value;
                if (!value)
                    preferences.LastValidated = null;
                store.SavePreferences(preferences);
                Evaluate(false);
            });
        }

        public void StartupPreference(bool value)
        {
            Post(delegate
            {
                if (readOnly)
                    return;
                LocalStore.SetStartup(value);
                preferences.StartWithWindows = value;
                store.SavePreferences(preferences);
                Evaluate(false);
            });
        }

        public void Restore()
        {
            SetEnabled(false);
        }

        void RequestTopology(string rebindId = null)
        {
            lock (gate)
            {
                if (closing)
                    return;
                if (rebindId != null)
                    rebindIds.Add(rebindId);
                if (topologyPending)
                    return;
                topologyPending = true;
                queue.Enqueue(delegate
                {
                    lock (gate)
                    {
                        topologyPending = false;
                    }

                    Reconcile();
                    Evaluate(true);
                });
                wake.Set();
            }
        }

        void RequestRefresh()
        {
            lock (gate)
            {
                if (closing || refreshPending)
                    return;
                refreshPending = true;
                queue.Enqueue(delegate
                {
                    lock (gate)
                    {
                        refreshPending = false;
                    }

                    Evaluate(true);
                });
                wake.Set();
            }
        }

        void Run()
        {
            int com = Native.CoInitializeEx(IntPtr.Zero, 0);
            try
            {
                Native.Check(com);
                enumerator = Native.CreateEnumerator();
                deviceEvents = new DeviceEvents
                {
                    Owner = this
                };
                Native.Check(enumerator.RegisterEndpointNotificationCallback(deviceEvents));
                Reconcile();
                Evaluate(true);
                Ready.Set();
                while (!stop.WaitOne(0))
                {
                    WaitHandle.WaitAny(new WaitHandle[] { stop, wake });
                    if (stop.WaitOne(0))
                        break;
                    Action action;
                    while (queue.TryDequeue(out action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            Fail(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            finally
            {
                Ready.Set();
                if (!readOnly)
                {
                    try
                    {
                        RestoreAll();
                    }
                    catch (Exception ex)
                    {
                        Fail(ex);
                    }
                }

                foreach (var w in watches.Values.ToList())
                    Detach(w);
                watches.Clear();
                foreach (var p in processes.Values)
                    p.Dispose();
                processes.Clear();
                if (enumerator != null)
                {
                    try
                    {
                        enumerator.UnregisterEndpointNotificationCallback(deviceEvents);
                    }
                    catch
                    {
                    }

                    Native.Release(enumerator);
                }

                if (com >= 0)
                    Native.CoUninitialize();
            }
        }

        void Fail(Exception ex)
        {
            store.Log("Audio operation failed: " + ex.GetType().Name + " 0x" + Marshal.GetHRForException(ex).ToString("X8"));
            Publish(new Diagnosis { Status = "Fix Requires Attention", Detail = "Windows could not complete an audio operation. Protection is not confirmed. Run diagnostics or exit and reopen EchoGuard.", Microphones = Current.Microphones, Selection = Current.Selection, ManualMode = preferences.SelectionMode == "Manual", SavedMicrophone = preferences.SelectedMicrophone, RememberConfiguration = preferences.RememberConfiguration, ProtectionEnabled = enabled });
        }

        void Reconcile()
        {
            List<string> rebind;
            lock (gate)
            {
                rebind = rebindIds.ToList();
                rebindIds.Clear();
            }

            foreach (string id in rebind)
            {
                Watch old;
                if (watches.TryGetValue(id, out old))
                {
                    Detach(old);
                    watches.Remove(id);
                }
            }

            inventory = AudioInventory.Read(enumerator);
            inventoryComplete = true;
            var active = new HashSet<string>(inventory.Where(e => e.State == 1).Select(e => e.Id));
            var liveKeys = new HashSet<string>();
            foreach (var info in inventory.Where(e => e.State == 1))
            {
                try
                {
                    Watch existing;
                    if (watches.TryGetValue(info.Id, out existing))
                    {
                        existing.Info = info;
                        Scan(existing, liveKeys);
                        continue;
                    }

                    Native.Device device;
                    Native.Check(enumerator.GetDevice(info.Id, out device));
                    object manager;
                    try
                    {
                        Guid iid = Native.SessionManagerId;
                        Native.Check(device.Activate(ref iid, 23, IntPtr.Zero, out manager));
                    }
                    finally
                    {
                        Native.Release(device);
                    }

                    var w = new Watch
                    {
                        Info = info,
                        Manager = (Native.Manager)manager
                    };
                    w.Created = new CreatedEvents
                    {
                        Owner = this,
                        Target = w
                    };
                    try
                    {
                        Native.Check(w.Manager.RegisterSessionNotification(w.Created));
                        watches[info.Id] = w;
                        Scan(w, liveKeys);
                    }
                    catch
                    {
                        Detach(w);
                        watches.Remove(info.Id);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    inventoryComplete = false;
                    store.Log("Endpoint inspection skipped: " + ex.GetType().Name);
                }
            }

            foreach (string id in watches.Keys.Where(id => !active.Contains(id)).ToList())
            {
                Detach(watches[id]);
                watches.Remove(id);
            }

            if (inventoryComplete && !readOnly && !store.InvalidJournal)
            {
                bool changed = false;
                foreach (string key in journal.Keys.Where(key => !liveKeys.Contains(key)).ToList())
                {
                    journal.Remove(key);
                    changed = true;
                }

                if (changed)
                    store.SaveJournal(journal);
            }
        }

        // A complete native enumeration proves that an old session-instance identifier no longer exists.
        // Only then may recovery records for ended sessions be discarded; partial scans retain them.
        void Scan(Watch w, HashSet<string> liveKeys)
        {
            Native.SessionList sessions;
            Native.Check(w.Manager.GetSessionEnumerator(out sessions));
            var present = new HashSet<string>();
            try
            {
                int count;
                Native.Check(sessions.GetCount(out count));
                for (int j = 0; j < count; j++)
                {
                    Native.Control c;
                    Native.Check(sessions.GetSession(j, out c));
                    string key;
                    try
                    {
                        Native.Check(((Native.Control2)c).GetSessionInstanceIdentifier(out key));
                    }
                    catch
                    {
                        Native.Release(c);
                        throw;
                    }

                    present.Add(key);
                    liveKeys.Add(Identity.Hash(w.Info.Id + "|" + key));
                    Add(w, c);
                }
            }
            finally
            {
                Native.Release(sessions);
            }

            foreach (string key in w.Sessions.Keys.Where(key => !present.Contains(key)).ToList())
                Remove(w, key, true);
        }

        static string ReadProperty(Native.Device d, string key, uint id)
        {
            try
            {
                return Native.Property(d, key, id);
            }
            catch
            {
                return "";
            }
        }

        void Add(Watch w, Native.Control control)
        {
            bool kept = false;
            try
            {
                if (!w.Active)
                    return;
                string instance;
                Native.Check(((Native.Control2)control).GetSessionInstanceIdentifier(out instance));
                if (w.Sessions.ContainsKey(instance))
                    return;
                var h = new Held
                {
                    Control = control,
                    Info = new SessionInfo
                    {
                        Key = instance
                    },
                    JournalKey = Identity.Hash(w.Info.Id + "|" + instance)
                };
                h.Events = new SessionEvents
                {
                    Owner = this,
                    Target = w,
                    Key = instance
                };
                Native.Check(control.RegisterAudioSessionNotification(h.Events));
                w.Sessions[instance] = h;
                kept = true;
                Update(h);
                h.LastMute = h.Info.Mute;
                h.LifecycleState = h.Info.State;
            }
            finally
            {
                if (!kept)
                    Native.Release(control);
            }
        }

        void Update(Held h)
        {
            int state;
            bool mute;
            uint pid;
            Native.Check(h.Control.GetState(out state));
            Native.Check(((Native.Volume)h.Control).GetMute(out mute));
            int hr = ((Native.Control2)h.Control).GetProcessId(out pid);
            Native.Check(hr);
            h.Info.State = state;
            h.Info.Mute = mute;
            h.Info.Pid = pid;
            h.Info.SingleProcess = hr == 0;
            h.Info.Discord = identity.Verify(pid, false);
            h.Info.Sonar = identity.Verify(pid, true);
            if (h.Info.Discord)
                WatchExit((int)pid);
        }

        void WatchExit(int pid)
        {
            if (processes.ContainsKey(pid))
                return;
            try
            {
                var p = Process.GetProcessById(pid);
                p.EnableRaisingEvents = true;
                p.Exited += delegate
                {
                    Post(delegate
                    {
                        Process existing;
                        if (processes.TryGetValue(pid, out existing) && Object.ReferenceEquals(existing, p))
                        {
                            processes.Remove(pid);
                            p.Dispose();
                        }

                        RequestRefresh();
                    });
                };
                processes[pid] = p;
            }
            catch
            {
            }
        }

        Diagnosis Evaluate(bool apply)
        {
            foreach (var w in watches.Values)
            {
                w.Info.Sessions.Clear();
                foreach (var h in w.Sessions.Values.ToList())
                {
                    try
                    {
                        Update(h);
                        w.Info.Sessions.Add(h.Info);
                    }
                    catch
                    {
                        inventoryComplete = false;
                        Remove(w, h.Info.Key);
                    }
                }
            }

            var d = MicrophoneSelection.Diagnose(inventory, preferences, Rules.Diagnose(watches.Values.Select(w => w.Info).ToList(), identity.DiscordCount(), Identity.OtherHelper()));
            d.ProtectionEnabled = enabled;
            if (!readOnly && preferences.SelectionMode == "Manual" && d.Selection.Microphone != null && d.Selection.Resolution == "Unique hardware, driver and endpoint-role match")
            {
                preferences.SelectedMicrophone = DeviceIdentity.From(d.Selection.Microphone);
                store.SavePreferences(preferences);
                d.SavedMicrophone = preferences.SelectedMicrophone;
            }

            if (!inventoryComplete)
            {
                d.Compatible = false;
                d.CanEnable = false;
                d.Candidates.Clear();
                d.Status = "Fix Requires Attention";
                d.Detail = "Windows did not provide a complete active-session inventory. No exclusion is authorized; run diagnostics again.";
            }

            if (store.InvalidJournal)
            {
                d.Status = "Fix Requires Attention";
                d.Detail = "The local restoration record is unreadable. No audio changes will be made. Exit EchoGuard and recover or move its local changes.json before retrying.";
                d.CanEnable = false;
                d.Candidates.Clear();
            }

            if (!readOnly && apply)
            {
                if (d.Conflict || !enabled || !d.Compatible)
                    RestoreAll();
                else if (d.Candidates.Count > 0)
                {
                    bool verified = true;
                    foreach (var w in watches.Values.Where(w => w.Info.Flow == 0 && w.Info.IsMic && w.Info.Id == d.TargetRenderId))
                    {
                        foreach (string key in d.Candidates)
                        {
                            Held h;
                            if (!w.Sessions.TryGetValue(key, out h))
                            {
                                verified = false;
                                continue;
                            }

                            Change change;
                            if (journal.TryGetValue(h.JournalKey, out change) && change.ExternalOverride)
                            {
                                verified = false;
                                continue;
                            }

                            try
                            {
                                bool before;
                                Native.Check(((Native.Volume)h.Control).GetMute(out before));
                                if (!before)
                                {
                                    if (!h.NeedsApplication)
                                    {
                                        verified = false;
                                        continue;
                                    }

                                    if (!journal.ContainsKey(h.JournalKey))
                                    {
                                        journal[h.JournalKey] = new Change
                                        {
                                            OriginalMute = before
                                        };
                                        store.SaveJournal(journal);
                                    }

                                    Guid context = Context;
                                    Native.Check(((Native.Volume)h.Control).SetMute(true, ref context));
                                    store.Log("Matched Discord microphone playback session excluded");
                                }

                                bool after;
                                Native.Check(((Native.Volume)h.Control).GetMute(out after));
                                h.NeedsApplication = false;
                                h.LastMute = after;
                                h.Info.Mute = after;
                                if (!after)
                                    verified = false;
                            }
                            catch
                            {
                                verified = false;
                            }
                        }
                    }

                    d.Status = verified ? "Protected" : "Fix Requires Attention";
                    if (verified && preferences.RememberConfiguration && d.Selection.Microphone != null)
                    {
                        var mic = d.Selection.Microphone;
                        var target = inventory.FirstOrDefault(e => e.Id == d.TargetRenderId);
                        if (target != null && (preferences.LastValidated == null || preferences.LastValidated.Microphone == null || preferences.LastValidated.Target == null || preferences.LastValidated.Microphone.EndpointId != mic.Id || preferences.LastValidated.Target.EndpointId != target.Id || preferences.LastValidated.SessionCount != d.Candidates.Count))
                        {
                            preferences.LastValidated = new ValidatedConfiguration
                            {
                                Microphone = DeviceIdentity.From(mic),
                                Target = DeviceIdentity.From(target),
                                ValidatedUtc = DateTime.UtcNow.ToString("o"),
                                SessionCount = d.Candidates.Count
                            };
                            store.SavePreferences(preferences);
                        }
                    }

                    d.Detail = verified ? "The matching Discord microphone playback session is confirmed muted. Sonar voice input and other playback sessions were left unchanged. Verify sound from another device." : "The targeted exclusion could not be confirmed, or another client changed its mute state. Disable and re-enable protection only after reviewing the audio routing.";
                }
            }

            if (!enabled && d.Candidates.Count > 0)
            {
                d.Status = "Ready to Apply Fix";
                d.Detail = "A compatible extra microphone path is present. Enable protection to apply the targeted exclusion.";
            }

            if (enabled && d.Candidates.Count == 0 && !d.Conflict && d.Compatible)
                d.Detail += " Protection is armed; no matching session has been changed.";
            Publish(d);
            return d;
        }

        void RestoreAll()
        {
            if (readOnly || store.InvalidJournal)
                return;
            if (Identity.OtherHelper() && journal.Count > 0)
            {
                store.Log("Restoration deferred: another audio helper is active");
                return;
            }

            foreach (var w in watches.Values)
            {
                foreach (var h in w.Sessions.Values)
                {
                    Change change;
                    if (!journal.TryGetValue(h.JournalKey, out change))
                        continue;
                    try
                    {
                        if (!change.ExternalOverride)
                        {
                            bool mute;
                            Native.Check(((Native.Volume)h.Control).GetMute(out mute));
                            if (ChangePolicy.ShouldRestore(change, mute))
                            {
                                Guid context = Context;
                                Native.Check(((Native.Volume)h.Control).SetMute(change.OriginalMute, ref context));
                                bool restored;
                                Native.Check(((Native.Volume)h.Control).GetMute(out restored));
                                if (restored != change.OriginalMute)
                                    throw new InvalidOperationException("Restore readback failed");
                                h.LastMute = restored;
                                store.Log("Owned session mute restored");
                            }
                        }

                        journal.Remove(h.JournalKey);
                    }
                    catch
                    {
                        store.Log("Restoration requires attention");
                    }
                }
            }

            store.SaveJournal(journal);
        }

        void VolumeNotice(Watch w, string key, bool mute, Guid context)
        {
            Held h;
            if (!w.Active || !w.Sessions.TryGetValue(key, out h))
                return;
            bool currentMute;
            Native.Check(((Native.Volume)h.Control).GetMute(out currentMute));
            if (!ChangePolicy.NotificationMatchesCurrent(mute, currentMute))
                return;
            if (context != Context && h.LastMute != mute)
            {
                Change c;
                if (journal.TryGetValue(h.JournalKey, out c))
                {
                    c.ExternalOverride = true;
                    if (!readOnly)
                        store.SaveJournal(journal);
                    store.Log("External session mute change observed; automatic reapplication suspended for this session");
                }
            }

            h.LastMute = mute;
            RequestRefresh();
        }

        void Remove(Watch w, string key, bool ended = false)
        {
            Held h;
            if (!w.Sessions.TryGetValue(key, out h))
                return;
            w.Sessions.Remove(key);
            try
            {
                h.Control.UnregisterAudioSessionNotification(h.Events);
            }
            catch
            {
            }

            Native.Release(h.Control);
            if (ended && !readOnly && journal.Remove(h.JournalKey))
                store.SaveJournal(journal);
        }

        void Detach(Watch w)
        {
            w.Active = false;
            try
            {
                w.Manager.UnregisterSessionNotification(w.Created);
            }
            catch
            {
            }

            foreach (string key in w.Sessions.Keys.ToList())
                Remove(w, key);
            Native.Release(w.Manager);
        }

        void Publish(Diagnosis d)
        {
            Current = d;
            if (lastStatus != d.Status)
            {
                store.Log(d.Status);
                lastStatus = d.Status;
            }

            var handler = Changed;
            if (handler != null)
            {
                try
                {
                    handler(d);
                }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (closing)
                    return;
                closing = true;
            }

            stop.Set();
            if (!thread.Join(10000))
                throw new TimeoutException("Audio worker is still restoring; do not remove local state yet.");
        }

        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        public sealed class CreatedEvents : Native.CreatedSink
        {
            internal AudioEngine Owner;
            internal Watch Target;
            public int OnSessionCreated(Native.Control c)
            {
                Owner.Post(delegate
                {
                    if (Target.Active)
                    {
                        Owner.Add(Target, c);
                        Owner.RequestRefresh();
                    }
                    else
                        Native.Release(c);
                });
                return 0;
            }
        }

        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        public sealed class DeviceEvents : Native.DeviceSink
        {
            internal AudioEngine Owner;
            public int OnDeviceStateChanged(string id, uint state)
            {
                Owner.RequestTopology(id);
                return 0;
            }

            public int OnDeviceAdded(string id)
            {
                Owner.RequestTopology(id);
                return 0;
            }

            public int OnDeviceRemoved(string id)
            {
                Owner.RequestTopology(id);
                return 0;
            }

            public int OnDefaultDeviceChanged(int f, int r, string id)
            {
                Owner.RequestTopology();
                return 0;
            }

            public int OnPropertyValueChanged(string id, Native.Key k)
            {
                Owner.RequestTopology();
                return 0;
            }
        }

        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        public sealed class SessionEvents : Native.EventSink
        {
            internal AudioEngine Owner;
            internal Watch Target;
            internal string Key;
            public int OnDisplayNameChanged(IntPtr n, IntPtr c)
            {
                return 0;
            }

            public int OnIconPathChanged(IntPtr n, IntPtr c)
            {
                return 0;
            }

            public int OnSimpleVolumeChanged(float v, bool m, IntPtr c)
            {
                Guid context = c == IntPtr.Zero ? Guid.Empty : (Guid)Marshal.PtrToStructure(c, typeof(Guid));
                Owner.Post(delegate
                {
                    Owner.VolumeNotice(Target, Key, m, context);
                });
                return 0;
            }

            public int OnChannelVolumeChanged(uint n, IntPtr v, uint ch, IntPtr c)
            {
                return 0;
            }

            public int OnGroupingParamChanged(IntPtr g, IntPtr c)
            {
                return 0;
            }

            public int OnStateChanged(int s)
            {
                Owner.Post(delegate
                {
                    Held h;
                    if (Target.Sessions.TryGetValue(Key, out h))
                    {
                        if (s == 1 && h.LifecycleState != 1)
                            h.NeedsApplication = true;
                        h.LifecycleState = s;
                    }

                    if (s == 2)
                        Owner.Remove(Target, Key, true);
                    Owner.RequestRefresh();
                });
                return 0;
            }

            public int OnSessionDisconnected(int r)
            {
                Owner.Post(delegate
                {
                    Owner.Remove(Target, Key, true);
                    Owner.RequestRefresh();
                });
                return 0;
            }
        }
    }
}
