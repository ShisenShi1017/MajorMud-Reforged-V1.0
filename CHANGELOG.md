# MajorMud Reforged Changelog

## Code Update 0001 — Repository Reorganization & Core Foundation

### Added
- New streamlined repository layout.
- `Core/` shared foundation folder.
- `Apps/` app-family folders for Launcher, Server, Editor, Importer, and Client.
- `Bin/Windows/` executable destination folders for each future app.
- `Updates/Code/` and `Updates/DatabaseVersions/` folders.
- Root `README.md` and `VERSION.md`.
- Documentation for the reorganization and application platform direction.

### Changed
- Prototype and obsolete root folders are moved into `Legacy/DeprecatedRoots/`.
- Old prototype launch scripts are moved into `Legacy/PrototypeLaunchers/`.
- PowerShell remains archived only as historical prototype material.
- Root README is replaced with the current update status.

### Protected
- `Data/` remains the active standalone database.
- `ImportedDatabases/` remains reference-only and is not overwritten.
- `Saves/` is not moved or overwritten.

### Not Included
- No compiled `.exe` is claimed or included.
- No .NET SDK installation is required.
- No server gameplay feature is implemented in this update.
