# Privacy and local data

EchoGuard works offline. It does not capture microphone samples, record or transmit audio, inject code, read private Discord configuration or send telemetry. There is no cloud dependency or update server. Vendor trust checks use offline/cache-only Windows verification.

Device names, IDs/states, default roles, hardware/driver/role/container metadata and audio-session/process identities are read locally.

With Remember enabled, settings.json in the user's local application data EchoGuard folder stores selection mode, microphone identity/display name, hardware/role/container properties, automatic processing/source preferences, consent, startup preference and last natively validated voice/target configuration. These values are never distributed. Remember off clears device/history and future protection consent; in-memory selection can continue.

changes.json contains hashed endpoint/session-instance keys and mute/override state for rollback. Recovery state persists independently of Remember until restoration or complete native proof the session ended. Bounded logs contain generic status/errors/timestamps without raw IDs, paths or sample data.

Save report exports confidence, resolution category, compatibility, counts, status and generic explanations. Device-list names/IDs/hardware properties are excluded. Nothing is uploaded.

Optional startup writes one per-user login entry. The installer adds its own files, shortcut and Apps registration. Uninstall preserves unrelated reports/files and refuses unresolved recovery records. See README for removal and restoration.
