# Security

EchoGuard 1.0.0 has no update server or telemetry. Report suspected vulnerabilities through [GitHub private vulnerability reporting](https://github.com/wopln/EchoGuard/security/advisories/new). Do not post account data, raw device identities, personal paths or diagnostic logs in public issues.

No administrator rights are needed. Release binaries are currently unsigned; checksums are provided, and code signing plus independent review remain planned. The utility verifies supported vendor executables using offline Windows trust checks before targeting their audio sessions. These checks cannot retrieve newer revocation information while offline.

If a restoration record is corrupt, EchoGuard blocks audio changes and preserves it. Keep the local recovery record until restoration is resolved. A same-user attacker can modify this user-level app as they can other user-level apps; EchoGuard is not a security boundary.

Version 1.0.0 supports only the documented Sonar/Discord route. Broader hardware/client versions, physical recreation, reboot/login, mixed-DPI movement and crash/recovery stress require additional validation. See the test and compatibility documentation.
