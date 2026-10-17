# Contributing

Build and test with the scripts in README. Keep native callbacks short, queued onto the MTA worker, and free of audio sample handling. Do not add endpoint default/gain enforcement or process injection. Changes to target detection must fail closed and include negative tests for unrelated sessions. Keep personal machine logs, paths and endpoint IDs outside the repository.

No telemetry, dependencies or network behavior may be added without an explicit documented design decision. Test audio changes using another device. Distinguish native state checks from listening results. Do not publish a release without maintainer approval; signing and broader compatibility checks remain pending.

Contributions are under the repository's MIT license. Version 1.0.0 is a public release for the documented supported route; do not expand compatibility claims without evidence.

