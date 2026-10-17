# EchoGuard 1.0.0 — release validation

Version 1.0.0 is prepared from the preserved 0.2.3 application. The application, installer, Settings and redacted diagnostic version labels are updated consistently. The original targeted session exclusion, detection, identity, ownership/recovery and selection algorithms are retained. Seven core source files are byte-for-byte unchanged; Models.cs changes only the diagnostic version string. The main window XAML and logo assets are unchanged.

94 regression checks and 61 local WPF/native-window checks passed. The release screenshot is rendered from the compiled WPF window using a temporary profile with protection disabled; it contains no device identities, accounts or personal paths. It is included in README, source/portable documentation and the installer payload. The installer adds/removes only its owned screenshot and preserves unrelated image files.

Application and installer file versions are 1.0.0.0, with informational/release version 1.0.0. GitHub Actions build/test packaging uses verified, commit-pinned official checkout/upload-artifact actions, read-only workflow token permissions and no secrets. Publishing is performed by the maintainer; CI does not automatically create releases.

The public repository uses a GitHub no-reply commit address. Runtime preferences, journals, logs, machine-specific paths/device identifiers, original reference artwork and old personal utilities are excluded. Known-identifier scans, archive integrity, checksum and embedded-payload checks are performed on the exact publishable files.

Builds are unsigned. Sonar metadata and Discord session topology remain vendor-specific assumptions, with no public flag proving a session contains only the duplicated microphone audio. Unsupported/ambiguous setups fail closed. Current tests do not certify every hardware or client configuration. See TESTING.md for the distinction between current automated checks, historical sound tests and pending manual acceptance. No user audio settings or installed application state were changed for publication.

MIT source and application licensing is preserved; Windows/.NET libraries are referenced from the installed runtime rather than redistributed. The old private versions remain available locally for rollback.
