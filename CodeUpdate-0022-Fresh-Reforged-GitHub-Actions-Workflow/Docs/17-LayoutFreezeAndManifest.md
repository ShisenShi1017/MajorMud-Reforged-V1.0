# 17 — Layout Freeze and Manifest System

## Decision

The repository layout is now frozen for active development. Future work should fit into the established structure instead of creating new top-level folders unless explicitly approved.

## Active top-level folders

- `Apps/` — application source folders
- `Core/` — shared models, serialization, validation, logging, configuration, event systems
- `Data/` — authoritative editable Reforged game database
- `ImportedDatabases/` — original/reference-only imports
- `Docs/` — project history and rules
- `Reports/` — generated audits and validation reports
- `Logs/` — runtime logs
- `Saves/` — protected save data
- `Tools/` — importer/updater/developer tools
- `Updates/` — Code Updates and Database Versions
- `Bin/` — built executables, each in its own folder
- `Backups/` — backups created during apply/update operations

## ProjectManifest.json

The root `ProjectManifest.json` is the machine-readable status file for update tools, future launcher tools, and future ChatGPT sessions. It records the current Code Update, Database Version, active layout, protected folders, and update rules.

## Cleanup policy

Duplicate or redundant files should be removed when there is a better canonical copy. Historical knowledge should remain in `Docs/`, `Reports/`, or version control instead of cluttering the active root.

## Executable policy

Every executable must live in its own folder. For Windows:

```text
Bin/Windows/Launcher/
Bin/Windows/Server/
Bin/Windows/Editor/
Bin/Windows/Importer/
Bin/Windows/Client/
```

No update may claim an `.exe` exists unless it has actually been built and included.
