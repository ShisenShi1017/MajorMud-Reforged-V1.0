# Code Update 0001 — Repository Reorganization & Core Foundation

## Summary

This update performs the approved cleanup of the Reforged repository and establishes the long-term app/core/bin layout.

## Apply behavior

The update is applied by running:

```bat
Apply-CodeUpdate-0001-Repository-Reorganization.bat
```

from its own extracted update folder.

The script attempts to locate the project root automatically. No path entry is required.

## Files and folders added

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
- `README.md`
- `CHANGELOG.md`
- `VERSION.md`
- new documentation under `Docs/`

## Folders moved to Legacy if present

- `Client-PC/`
- `Client-Android/`
- `HostEditor-PC/`
- `Source/`
- `SharedData/`
- `PatchFiles/`
- `PatchBackup/`
- `DatabaseVersionBackup/`

## Root scripts moved to Legacy if present

- `Server.ps1`
- `Client.ps1`
- `Run-Server-Only.bat`
- `Test-LocalClient.bat`

## Protected

- `Data/`
- `ImportedDatabases/`
- `Saves/`

## Notes

No `.exe` is included or claimed by this update.
No .NET SDK installation is required.
