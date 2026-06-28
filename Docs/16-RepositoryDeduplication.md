# Repository Deduplication

Code Update 0002 removes redundant prototype and duplicate files from the active repository.

## Removed

- Duplicate `README.txt`
- Old PowerShell prototype archive under `Legacy/PowerShellPrototype`
- Old prototype launchers under `Legacy/PrototypeLaunchers`
- Deprecated legacy placeholder roots for old client/editor/source layouts
- Old generated backup snapshots under `Backups/`
- Obsolete duplicate changelog files under `Docs/`
- Old runtime `Logs/server.log`

## Preserved

- `Apps/`
- `Core/`
- `Data/`
- `ImportedDatabases/`
- `Docs/`
- `Reports/`
- `Saves/`
- `Tools/`
- `Updates/`
- `Bin/`

## Reason

The project is now past the prototype folder migration stage. Redundant archives made the repository harder to upload, review, and navigate. The cleaned structure keeps only active project requirements plus the original database/reference material needed for conversion and verification.
