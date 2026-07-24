# Client Bundle 42 — Installer, Updater, and Release Packaging

Bundle 42 completes the cumulative Client delivery tooling.

Run `INSTALL_VALIDATE_AND_PACKAGE_CLIENT_BUNDLE_42.bat` from the bundle's own top folder and pass the main repository path. The installer validates every cumulative source file against the Bundle 42 manifest, optionally backs up repository files that will be replaced, installs only the cumulative Client files, verifies the installed hashes, runs static validation when Python is available, builds when the .NET 8 SDK is available, and publishes a Windows x64 release package when possible.

Generated repository artifacts are placed under `Artifacts/ClientBundle42`. Replaced files are backed up under `.client-bundle-backups/bundle42-<timestamp>` unless `-NoBackup` is supplied.

Useful options:

- `-SkipBuild` — install and statically validate without compiling.
- `-SkipPublish` — build but do not create the release ZIP.
- `-NoBackup` — do not create the rollback backup.
- `-Configuration Debug` — use a non-default build configuration.

The tooling never changes Host gameplay data, world data, Editor content, Librarium records, or user Client profiles.
