# Reforged

**Current status:** Repository layout frozen for Core and Server development.

## Current Versions

- Current Code Update: **CodeUpdate-0003 — Layout Freeze, Manifest, and Redundancy Cleanup**
- Current Database Version: **DatabaseVersion-0009 — Server-Ready Data Baseline**
- Current milestone: **Core + Standalone Server Bootstrap**

## What changed in this update

- Added root `ProjectManifest.json` as the machine-readable project status file.
- Standardized the active repository layout.
- Removed redundant duplicate `README.txt` files where matching `README.md` files already exist.
- Removed old generated apply reports that are no longer required as active repository files.
- Kept all required database, save, source, docs, tools, and update folders.
- Confirmed executable outputs belong in their own folders under `Bin/Windows/`.

## Active layout

```text
Apps/             Application source folders: Launcher, Server, Editor, Importer, Client
Backups/          Project backups created by update/apply tools
Bin/              Future built executables, each in its own folder
Core/             Shared models, validation, serialization, logging, configuration
Data/             Reforged editable standalone game database
Docs/             Project history, architecture, rules, handoff, and design notes
ImportedDatabases/ Original/reference-only imports; never overwritten
Logs/             Runtime and validation logs
Reports/          Audit and validation reports
Saves/            Player/server saves; never overwritten
Tools/            Importer, updater, validation, and developer tools
Updates/          Code Updates and Database Versions
```

## Important rules

- The server must load from `Data/`, not from `.DAT`, `.mdb`, `.accdb`, or CSV files.
- `ImportedDatabases/` is reference-only after conversion.
- `Saves/` and `ImportedDatabases/` must not be overwritten by updates.
- Every Code Update and Database Version must run from its own extracted folder.
- Every update must update this README and the docs.
- Executables must live in their own folders under `Bin/Windows/`.
- No executable is claimed unless it has actually been created.

## Next development step

Begin **Core Foundation** implementation, then build the standalone server around Core.
