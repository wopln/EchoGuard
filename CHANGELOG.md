# Changelog

## 1.0.0 — 2026-10-17

First public release, built from the tested 0.2.3 application.

- Targeted, reversible Sonar/Discord Entire Screen microphone-session protection.
- Automatic/manual microphone selection, identity persistence, confidence and safe routing gates.
- Native session/device monitoring, optional startup, tray and restoration controls.
- Responsive dark WPF interface with native dragging/resizing/maximize/restore/Snap and branded Windows icon.
- Public source, Windows installer, portable package, checksums and application screenshot.
- Unified application/installer/diagnostic version labels to 1.0.0; audio mutation and selection algorithms retained.
- 94 regression checks and 61 local WPF/native-window checks passed. Unsigned builds, vendor-specific compatibility and pending listening/hardware/reboot/DPI validation are documented.
## 0.2.3 Icon Preview — 2026-10-17

- Embedded the existing blue vector EchoGuard symbol as a transparent multi-resolution Windows icon.
- Applied it to executable/Shell, taskbar/window, tray and installer; no audio or layout changes.
- Preserved the 0.2.2 release and built a separate private preview.

## 0.2.2 Window Preview — 2026-10-17

- Replaced a zero-height native caption and mouse-event DragMove fallback with a measured WindowChrome caption; title buttons remain client-interactive.
- Removed fixed page dimensions and whole-page Viewbox scaling. Grids stretch horizontally, shorter windows use compact spacing, and constrained work areas scroll vertically.
- Preserved reference-size anchors and native vector/theme styling. Minimum is 850 x 670 DIPs, monitor work area permitting.
- Added monitor-aware maximized work-area bounds, PerMonitorV2 manifest awareness and restore-state preservation when reopening from the tray/taskbar.
- Sized the microphone popup to its actual selector to prevent overflow.
- 94 existing tests and 61 WPF/native-window checks passed. Desktop dragging, corner resizing, header maximize/restore, right-edge Snap, title controls and tray behavior were exercised.
- Audio/core code and user audio settings were not changed. Actual 125%/150% Windows scaling and a new listening test remain pending.
## 0.2.1 UI Preview — 2026-10-17

- Redesigned the latest 0.2.0 WPF UI to the supplied reference; preserved the original release in a separate directory.
- Added borderless rounded window, draggable header, custom minimize/close, native vector logo/icons and navy/blue resources.
- Reduced the main screen to microphone, status, Detect Echo, Fix Echo, startup and help; moved diagnostics, Remember, identities, reports, restore and Exit completely into Settings.
- Preserved all 0.2 auto/manual selection, identity persistence, native monitoring, startup/tray, safety and protection services without changing audio/core source files.
- Added honest initial/analyzing/supported/protected/unsupported/error presentation and keyboard focus treatments. Initial background discovery does not enable Fix Echo.
- Compared actual desktop captures and native WPF renders against the reference; corrected positions, typography and colors over multiple iterations.
- Added 16 presentation tests and 24 WPF layout/render/lifecycle checks; reran 13 native checks. No overlapping protection or disruptive audio restart was used.
- Remains private and unsigned. Fresh audible verification and actual mixed-monitor DPI/reboot/hardware tests are not claimed.

## 0.2.0-preview — 2026-10-17

- Extended the existing 0.1 implementation; preserved its targeted ISimpleAudioVolume exclusion and rollback ownership policy.
- Enumerates recording endpoints in all Windows states and shows input/communications defaults, local IDs and cautious classification.
- Added Auto Detect / manual microphone selection, Confirmed/Suggested/Unknown confidence and explicit unsupported/mismatch explanations.
- Separates voice capture, processing and duplicate playback source. Selection never mutes the input, changes a default or applies exclusion by itself.
- Added strict identity persistence and unique hardware/driver/role/container recovery; rejects ambiguous names, changed identity and unsupported sources.
- Added Remember controls, validation history, startup preference retention and safe legacy preference migration. Selection changes restore owned changes and disable protection.
- Added dark microphone selector, available-device details, Detect Echo Source, targeted-path display and always-visible tray/exit controls.
- Added simulated multi-device/reconnection/restart tests and real native detection/configuration checks. No overlapping protector was used.
- Updated installation, privacy, detection, rollback and troubleshooting documentation. Remains private and unsigned.

## 0.1.0-preview — preserved release

Original standalone extraction of the successfully tested Sonar/Discord Entire Screen exclusion, native event-driven protection, ownership journal, tray interface, installer and MIT source. Independent listening and Discord restart tests passed on one PC. Reboot/Sonar/USB and broader compatibility remained pending.

