# Mechanism, limits and safety

## What the working fix actually does

The original successful fix was studied before extraction. It finds Discord's extra session on Sonar's internal microphone **render** endpoint, then sets that session's `ISimpleAudioVolume` mute. Discord's voice session on the paired **capture** endpoint stays untouched. Game/music playback endpoints stay untouched.

It does **not** use `ActivateAudioInterfaceAsync` or process-loopback include/exclude parameters to modify Discord's existing capture stream. Those parameters belong to the new interface activated by the caller; they are not an editor for another application's already-open stream. [Microsoft process-loopback parameters](https://learn.microsoft.com/en-us/windows/win32/api/audioclientactivationparams/ns-audioclientactivationparams-audioclient_process_loopback_params).

Windows does not retain loopback-session mute settings as persistent render-session settings. Native events are therefore used for new sessions and activation cycles. [Microsoft audio sessions](https://learn.microsoft.com/en-us/windows/win32/coreaudio/audio-sessions).

## APIs and threading

- `IMMDeviceEnumerator`, `IMMDevice`, `IMMEndpoint`, `IPropertyStore`: discover active endpoints and read metadata.
- `IMMNotificationClient`: endpoint add/remove/state/default/property notifications. Defaults are observed only.
- `IAudioSessionManager2`, `IAudioSessionEnumerator`, `IAudioSessionControl2`: session identity, process and state.
- `IAudioSessionNotification` and `IAudioSessionEvents`: lifecycle and mute notifications.
- `ISimpleAudioVolume::GetMute/SetMute`: the only audio mutation. No endpoint gain, endpoint mute or default-device setter exists in the app.
- `WinVerifyTrust` with cache-only trust retrieval, and signed-file publisher inspection: offline validation of supported running binaries. Offline verification may reject a valid binary if necessary trust data is absent; it does not fetch missing certificates.

A dedicated MTA worker owns audio interfaces. The WPF STA dispatcher owns the UI. Callbacks enqueue work and return promptly. `GetCount` is called after notification registration to enable session notifications. [Microsoft session notification requirements](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessionmanager2-registersessionnotification).

There are no audio-client/capture-client interfaces, sample buffers, drivers, services, sockets, HTTP clients, Discord file changes, injection, or continuous polling. A named per-user mutex avoids duplicate instances; named events request show/restore-and-exit from a running instance.

## Protection and restoration

Changes require user opt-in and the complete known-path diagnosis. Protection is applied on explicit enable, session creation or a new activation cycle. A native readback must confirm mute before **Protected** appears. Ordinary volume notifications do not trigger enforcement.

Before setting mute, an atomic local journal records the previous state, keyed by a hash of endpoint/session-instance identity. Pre-existing mutes are not claimed as changes. A delayed notification is ignored when its value no longer matches current native state. If an external current mute change is observed, automatic reapplication for that session is suspended and attention is reported. The app cannot attribute that change to a person or process.

Disable/restore/exit attempts to undo only owned changes and preserves observed external overrides. The recovery record remains if restoration cannot be confirmed. Uninstall refuses to discard an unresolved journal. A complete native session enumeration can clear records for session instances that no longer exist; partial scans retain them. Endpoint lifecycle events force a rebind even if Windows reuses an endpoint ID. Crash recovery and physical recreation still need broader validation.

## Unsupported assumptions and alternatives

The Windows COM APIs are documented. Sonar driver identity plus `ChatCapture` property metadata is **vendor-specific**, not a public Sonar contract. No documented session flag tells this controller that Discord's matching session is exclusively screen-share loopback. Muting a differently implemented future session could affect other behavior. This version therefore uses exact routing/publisher/session gates and still requires remote listening verification.

No undocumented `IPolicyConfig`, personal endpoint identifiers, personal default-device assignments or physical microphone volume code was extracted. The known personal helper is blocked to avoid overlapping ownership.

A safer universal source-selection alternative was investigated: application-only sharing changes the user's required full-screen workflow; the installed Discord picker offered no independent full-screen audio exclusion. SteelSeries' documented streamer recipe changes the voice mix and disables stream audio, contrary to preserving independent game audio. None replaces the requested behavior. A vendor-supported Discord exclusion API would be preferable if one becomes available. [SteelSeries recipe](https://support.steelseries.com/hc/en-us/articles/35145998503181-Setup-Sonar-to-Stream-with-Discord-Without-Echo).

Remaining validation work: Authenticode signing, clean-machine installer/security review, broader hardware/client versions, physical endpoint recreation, reboot/login testing, crash/recovery stress tests and Sonar restart recovery validation. Release 1.0.0 retains these limits and does not claim certification or universal compatibility.



## 0.2 selection gates

The original mutation/rollback block remains. Selection.cs now authorizes it only when the selected input matches the verified unique Sonar voice endpoint. AudioInventory.cs reads all endpoint states/default input roles. Selection changes restore owned changes and disable consent; persisted history never bypasses current detection. See SMART_DETECTION.md and TESTING.md for upgrade evidence and pending fresh live checks.

