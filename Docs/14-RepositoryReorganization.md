# Repository Reorganization

## Purpose

Code Update 0001 cleans the repository so future server, importer, editor, launcher, and client work has a stable structure.

The project had accumulated prototype folders, old PowerShell scripts, and transitional update folders. Those are preserved under `Legacy/`, not deleted.

## Active architecture

```text
Apps/       App-specific source and assets
Core/       Shared data models, validation, import contracts, runtime contracts
Data/       Active editable standalone Reforged database
ImportedDatabases/ Original source/reference imports only
Docs/       Project knowledge and development history
Reports/    Validation/import/decode reports
Saves/      Player/server save data; protected
Tools/      Developer and maintenance tools
Updates/    Code Update and Database Version metadata
Bin/        Future compiled executables, each in its own folder
Legacy/     Preserved obsolete/prototype material
```

## Legacy movement rule

Folders that are not part of the active architecture are moved to:

```text
Legacy/DeprecatedRoots/<OriginalFolderName>/
```

Old root PowerShell/BAT prototype launchers are moved to:

```text
Legacy/PrototypeLaunchers/
```

## Protected folders

The cleanup does not move or overwrite:

- `Data/`
- `ImportedDatabases/`
- `Saves/`

## Executable rule

Every future executable must live in its own folder:

```text
Bin/Windows/Launcher/
Bin/Windows/Server/
Bin/Windows/Editor/
Bin/Windows/Importer/
Bin/Windows/Client/
```

Code Update 0001 creates those folders but does not claim any executable exists.
