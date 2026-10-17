# Testing — EchoGuard 1.0.0

## Current release checks

| Area | Result |
| --- | --- |
| Pure routing, selection, identity, ownership, persistence and status checks | 94 passed |
| Local WPF/native-window checks with a temporary profile and protection disabled | 61 passed |
| Build | Application and per-user installer compiled successfully |
| Icon and screenshot | Embedded icon loads in the real WPF/tray harness; screenshot rendered from the 1.0.0 build |
| Audio mutation/selection algorithms | Retained from 0.2.3; only diagnostic version text changes in Models.cs |
| Installer payload | Application, configuration, documentation and screenshot resource hashes checked before publication |
| Package integrity/privacy | ZIP integrity, checksums and known personal identifier/runtime-state scan checked before publication |

The 94 checks cover fail-closed routing and publisher/session gates, unrelated playback rejection, consent, ownership restoration, external changes, malformed journals, multiple inputs, manual/auto suggestions, duplicate names, identity mismatch/rebinding, simulated unplug/reconnect and Discord/session restarts, persistence and presentation gates.

The 61 WPF checks cover caption/client/resize hit tests, microphone choices and popup sizing, reference/minimum/wide/maximized layouts, short-work-area scrolling, readable fonts, monitor work-area maximization, restore bounds/state, Settings/Help, startup reflection, tray reopening and zero owned audio changes. Render targets cover 100%, 125% and 150%; this is not a test of changing Windows scaling.

Run `scripts/test.ps1` for the pure suite. Run `scripts/test-ui.ps1` on an unlocked Windows desktop for the WPF harness. CI builds packages and runs the pure suite. Historical native/live harnesses have setup requirements, including controlled sharing and other protectors stopped; do not run them blindly.

## Prior evidence, not fresh 1.0.0 sound tests

The extracted original fix passed separate-device listening with the normal mic unmuted/muted, game/music preserved, repeated Entire Screen sharing and Discord restart. Later development passed 13 native inventory/configuration checks, exposing 20 recording endpoints including inactive devices. Desktop interaction checks covered header dragging, corner minimum resizing, maximize/restore, right-edge Snap, Settings, microphone menu, minimize and close-to-tray. Earlier installer checks covered registration/payload cleanup and unrelated-file preservation.

Those are prior-version checks. They are not presented as fresh listening, native inventory or clean-machine installer tests for 1.0.0. The current release retains the mechanism and passes its regressions.

## Pending manual acceptance

Fresh second-device listening with only EchoGuard enabled; active Fix Echo sound verification on the final binary; Sonar restart; real USB unplug/reconnect and endpoint recreation; reboot/logout-login; actual 125%/150% scaling and mixed-DPI monitor movement; broader hardware/client versions; clean-machine installer/security/signing review and crash-recovery stress.

For listening: start Entire Screen sharing with audio, select the actual Sonar voice input, Detect Echo and enable only a supported fix. From another device using headphones, check voice once with the mic unmuted and game/music audible; mute normal Discord voice and confirm microphone audio is absent from the stream while music remains. Repeat after Discord restart. Same-PC stream playback can cause feedback and is not independent verification.
