# Changelog

## Code Update 0002 - Repository Deduplication

- Removed duplicate root `README.txt` in favor of `README.md`.
- Removed redundant legacy PowerShell prototype archive.
- Removed obsolete legacy client/editor/source placeholder folders.
- Removed old backup snapshots that should not remain in the active repository tree.
- Removed duplicate/obsolete changelog files from `Docs/`.
- Added `.gitignore` for runtime logs, local backups, temporary reports, and build artifacts.
- Preserved `Data/`, `ImportedDatabases/`, `Saves/`, `Apps/`, `Core/`, `Docs/`, `Tools/`, `Updates/`, and `Bin/`.
- Updated documentation and handoff status.

## Code Update 0001 - Repository Reorganization & Core Foundation

- Established the streamlined repository layout.
- Created `Apps/`, `Core/`, `Bin/`, `Updates/`, and supporting folders.
- Moved prototype-era folders into `Legacy/` instead of deleting them.
