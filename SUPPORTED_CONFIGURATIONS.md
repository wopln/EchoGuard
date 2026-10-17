# Supported configurations — 1.0.0

Windows 11 x64, .NET Framework 4.8+, official signed stable Discord and the known Sonar microphone capture/render pair. The underlying 0.1 live fix was tested with GG 121 / Sonar 1.104 / Discord 1.0.9262 on Windows build 26200. The 0.2 detection/selection upgrade was tested on that PC; fresh 0.2 listening/restart verification remains pending.

Discovery and manual selection cover the recording endpoints Windows exposes, including unavailable devices. Classification stays Unknown unless relevant metadata supports a more specific label. Recognition of NVIDIA Broadcast, other mixers, webcam or USB inputs does not imply echo-fix support.

Before exclusion, the selected endpoint must match a unique verified active Discord Sonar voice session. A unique related Sonar microphone render endpoint, active verified Sonar writer, same-process single-process Discord render target and no other active Discord recording endpoint must also be present. Conflicting helpers, partial inventories, malformed recovery state and ambiguous identities block changes.

The public Windows APIs are documented. Sonar's driver/ChatCapture metadata and Discord's session topology are vendor-specific assumptions, not a public compatibility contract. No documented flag proves a session is exclusively screen sharing; remote listening verification remains necessary. Browser Discord, PTB/Canary/repacked clients, arbitrary virtual mixers, generic acoustic echo, same-PC stream playback, other operating systems and ARM64 are not certified.

Endpoint ID recreation is recovered only through strict unique metadata matching. Some devices lack enough evidence and need manual reselection. Windows defaults are read, not changed. Future vendor releases can invalidate assumptions; disable protection if behavior differs.
