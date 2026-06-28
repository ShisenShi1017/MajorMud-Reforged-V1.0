# MajorMud Reforged

## Current status

**Current milestone:** Standalone server foundation preparation  
**Current database baseline:** Server-ready editable `Data/` from CSV + MegaMud `.mp` + native runtime references  
**Current code update:** Code Update 0002 - Repository Deduplication

MajorMud Reforged is being developed as a standalone MajorMud-compatible server with a future real-time editor and client inspired by MegaMud 2.0.

## Active repository layout

- `Apps/` - future standalone applications: Launcher, Server, Editor, Importer, Client
- `Core/` - shared game/data/validation/runtime foundation
- `Data/` - Reforged's editable game database and server loading source
- `ImportedDatabases/` - original MajorMud source/reference files only
- `Docs/` - project vision, architecture, rules, handoff, release notes, database history
- `Reports/` - generated validation/audit reports
- `Saves/` - player save data; never overwritten by updates
- `Tools/` - importer/update tooling
- `Updates/` - Code Updates and Database Versions
- `Bin/` - future compiled executables, each in its own folder
- `Backups/` - local runtime backups only
- `Logs/` - runtime logs only
- `Legacy/` - no longer active; kept only for a short note unless future migration evidence is needed

## What changed in Code Update 0002

This update removed redundant prototype and duplicate repository files from the active tree:

- Removed duplicate `README.txt`; `README.md` is now the only root readme.
- Removed old PowerShell prototype archives from `Legacy/`.
- Removed deprecated old client/editor/source placeholders from `Legacy/DeprecatedRoots/`.
- Removed old repository backup snapshots from `Backups/`.
- Removed obsolete duplicate changelog files from `Docs/`.
- Added `.gitignore` rules to keep generated logs/backups out of normal repository uploads.
- Preserved `Data/`, `ImportedDatabases/`, and `Saves/`.

## Update rules

Every Code Update or Database Version must:

1. Run from its own extracted update folder.
2. Include an `Apply-*.bat` until the compiled launcher/updater exists.
3. Auto-locate the project root without requiring a typed path.
4. Update this `README.md`.
5. Update `CHANGELOG.md`, `VERSION.md`, and relevant docs.
6. Preserve `Saves/` and `ImportedDatabases/` unless explicitly approved otherwise.
