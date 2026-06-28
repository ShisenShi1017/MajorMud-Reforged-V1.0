# MajorMud Reforged

## Current Status

**Current milestone:** Code Update 0001 — Repository Reorganization & Core Foundation  
**Primary goal:** Build a completely standalone MajorMud server, editor, importer, launcher, and future client using the Reforged `Data/` folder as the active game database.

## What changed in this update

This update performs the approved repository cleanup before deeper server development begins.

- Creates the streamlined long-term layout:
  - `Apps/Launcher/`
  - `Apps/Server/`
  - `Apps/Editor/`
  - `Apps/Importer/`
  - `Apps/Client/`
  - `Core/`
  - `Bin/Windows/Launcher/`
  - `Bin/Windows/Server/`
  - `Bin/Windows/Editor/`
  - `Bin/Windows/Importer/`
  - `Bin/Windows/Client/`
  - `Updates/Code/`
  - `Updates/DatabaseVersions/`
- Moves obsolete prototype folders and scripts into `Legacy/` instead of deleting them.
- Keeps `Data/` as the active standalone database.
- Keeps `ImportedDatabases/` as source/reference only.
- Keeps `Saves/` protected.
- Establishes `Core/` as the shared foundation for the server, editor, importer, launcher, and future client.
- Begins moving away from PowerShell prototypes toward compiled applications.
- Adds/update documentation so future chats and developers can continue from the current state.

## Active top-level layout

```text
MajorMud-Reforged/
├── Apps/
│   ├── Launcher/
│   ├── Server/
│   ├── Editor/
│   ├── Importer/
│   └── Client/
├── Bin/
│   └── Windows/
│       ├── Launcher/
│       ├── Server/
│       ├── Editor/
│       ├── Importer/
│       └── Client/
├── Core/
├── Data/
├── ImportedDatabases/
├── Docs/
├── Reports/
├── Saves/
├── Tools/
├── Updates/
├── Backups/
├── Legacy/
├── README.md
├── CHANGELOG.md
└── VERSION.md
```

## Important development rules

- `Data/` is the live editable Reforged database.
- `ImportedDatabases/` is reference/fallback only after conversion.
- Do not overwrite `Saves/`.
- Do not overwrite original imported databases.
- All Code Updates and Database Versions must run from their own extracted folder.
- Every update must update this root README and the documentation in `Docs/`.
- Do not claim an executable exists unless it has actually been built and included.
- Executables must live in their own folder under `Bin/Windows/<AppName>/`.

## Next goal

Begin **Code Update 0002 — Core Data Loader & Server Bootstrap** using the new layout.

That update should make the server load only from `Data/`, validate the database, build an in-memory world model, and shut down cleanly without depending on `.DAT`, `.mdb`, `.accdb`, CSV, or PowerShell runtime files.
