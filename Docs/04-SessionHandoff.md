# Session Handoff

## Current project state

The project has moved from prototype scripting and import experimentation into the official application architecture phase.

## Latest completed update

**Code Update 0001 — Repository Reorganization & Core Foundation**

## What this update does

- Creates the final streamlined top-level layout.
- Moves obsolete prototype folders and old root scripts into `Legacy/`.
- Establishes `Core/` as the shared foundation.
- Creates app folders under `Apps/`.
- Creates future executable folders under `Bin/Windows/`.
- Replaces the root README with the current project status.
- Adds `CHANGELOG.md` and `VERSION.md`.

## Current active source of truth

- Code source of truth: the current repository after this update is applied.
- Data source of truth: `Data/`.
- Imported data source/reference: `ImportedDatabases/`.

## Next recommended step

**Code Update 0002 — Core Data Loader & Server Bootstrap**

Implement:
- Core data manifest model.
- Core JSON loading helpers.
- Core validation result model.
- Server bootstrap that loads `Data/` only.
- Validation report output under `Reports/` or `Logs/`.

## Important reminder

Do not require the user to install the .NET SDK or developer tools for normal use. Until prebuilt executables exist, do not claim executables exist.
