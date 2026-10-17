# Smart detection and identity

## Native inventory and confidence

IMMDeviceEnumerator::EnumAudioEndpoints(eAll, DEVICE_STATEMASK_ALL) enumerates both flows and all endpoint states. The UI lists recording endpoints. IMMDevice::GetState supplies active, disabled, not-present and unplugged states. GetDefaultAudioEndpoint(eCapture, role) reads console, multimedia and communications roles without changing them.

IPropertyStore reads friendly name, existing Sonar driver/role metadata, associated device-instance identity and container GUID where available. Container values support VT_CLSID. USB-backed classification uses reported hardware bus identity; Sonar virtual classification uses the known driver. Other devices remain Unknown. Names do not prove physical/virtual classification or an upstream processing connection.

The existing IAudioSessionManager2 enumeration/notifications and IAudioSessionControl2 supply session metadata. One active, unmuted, single-process recording session belonging to an offline-verified official Discord binary confirms observed capture routing. It does not prove the client's private input setting, session purpose, processed audio quality, or audible echo. Multiple active recording routes are ambiguous.

Without confirmed input, communications default, then multimedia/console default or only available input may be Suggested. A suggestion cannot authorize muting. Manual choices are diagnosed without following another endpoint silently.

## Three separate identities

1. Selected/observed voice capture endpoint.
2. Optional processing system: only the supported Sonar driver/writer relationship is recognized.
3. Separate duplicate-source render session: the original gated Discord session on Sonar's internal microphone playback endpoint.

A physical input that differs from Sonar voice is not assumed to be its upstream source. Other virtual mixers, webcam and USB inputs can be enumerated/selected, but do not gain echo-fix support simply by coexisting with Sonar.

All original publisher, unique pair, matching root, same process, single-process target, Sonar writer, active session and no-other-Discord-capture gates remain. A complete active-session inventory and matching selected/confirmed Sonar input are also required. Selection changes first restore owned changes and turn protection off. Detect Echo Source does not opt in. If protection is already enabled, a native refresh can maintain only a verified route.

## Identity persistence

Local preferences store endpoint ID/display name, associated hardware, driver, role and container where available. These user-specific values are not in reports or release files. Ordinary endpoint IDs are not immutable.

An active exact ID is rejected if saved nonempty identity properties changed. A name can change without losing identity. A disabled exact endpoint stays unavailable. If an ID is unavailable, recreation matching requires nonempty hardware, driver and role, the capture flow, saved container if present, and exactly one available match. There is no name matching, broad USB model matching or arbitrary pin choice. Ambiguity/insufficient evidence requires user selection. A successful identity recovery updates the saved ID; full live routing gates still apply.

The last validated record contains voice/target identities, system, time and target count only after native mute readback. It never bypasses fresh detection. Remember off clears saved selection/history and future protection consent. Necessary hashed recovery records remain until restoration. Login registration stays independently user controlled.

Native device add/remove/state/property/default notifications refresh inventories; lifecycle events rebind session managers. Session creation/activation and verified Discord process exit refresh detection. No polling timer exists. Real cross-hardware recreation and reboot checks remain pending; synthetic tests are not certification.

## References

- [Microsoft device properties](https://learn.microsoft.com/en-us/windows/win32/coreaudio/device-properties)
- [Microsoft endpoint states](https://learn.microsoft.com/en-us/windows/win32/coreaudio/device-state-xxx-constants)
- [Microsoft endpoint ID limitations](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-id-strings)
- [Microsoft session notification requirements](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessionmanager2-registersessionnotification)

ARCHITECTURE.md documents the underlying API and vendor assumptions. Process-loopback exclusion parameters cannot edit another application's already-open capture stream.
