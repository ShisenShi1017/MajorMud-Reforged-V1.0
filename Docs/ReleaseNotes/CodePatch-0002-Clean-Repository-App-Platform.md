# CodePatch 0002 - Clean Repository / App Platform Foundation

## Summary

This update starts the long-term transition from a PowerShell prototype toward a cleaner standalone MajorMud Reforged application platform.

## Added

- `Apps/Launcher/`
- `Apps/Server/`
- `Apps/Editor/`
- `Apps/Importer/`
- `Apps/Client/`
- `Bin/Windows/<AppName>/` future executable folders
- `Source/Core/`
- `Source/Server/`
- `Source/Editor/`
- `Source/Importer/`
- `Source/Launcher/`
- `Source/Client/`
- `Docs/12-CleanRepositoryPlan.md`
- `Docs/13-AppPlatformVision.md`
- `Tools/UpdateManager/README.txt`

## Changed

- Root `README.txt` is replaced with an update-focused project overview.
- `Docs/10-ChatRules.md` now includes the no-SDK user requirement and executable-folder rule.
- Prototype PowerShell files are moved to `Legacy/PowerShellPrototype/` by the apply script instead of being deleted.

## Protected

The apply script does not overwrite or move:

- `Saves/`
- `ImportedDatabases/`
- `Data/`

## Important note

No executable is claimed in this update. The executable folders are created now so future builds have clean, dedicated locations.
