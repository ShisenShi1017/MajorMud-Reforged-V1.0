# Code Update 0002 Redundancy Audit

## Redundant items selected for removal

| Path | Reason |
|---|---|
| `README.txt` | Duplicate of root `README.md`; GitHub should use Markdown. |
| `Logs/server.log` | Generated runtime file, not source. |
| `Docs/CHANGELOG.txt` | Superseded by root `CHANGELOG.md`. |
| `Docs/CHANGELOG-v2.9-EDITOR-DATA-PATCH.txt` | Old pre-standardization changelog. |
| `Docs/ReleaseNotes-DatabaseVersion-0005.md` | Duplicate legacy placement; release notes belong under `Docs/ReleaseNotes/`. |
| `Legacy/PowerShellPrototype/` | Prototype archive no longer active. |
| `Legacy/PrototypeLaunchers/` | Superseded launchers; future EXEs live under `Bin/Windows/`. |
| `Legacy/DeprecatedRoots/*` | Old root structure already replaced by `Apps/`, `Core/`, and `Bin/`. |
| `Backups/CodeUpdates/` | Generated local backup output, not active source. |
| `Backups/Data-before-*` | Generated local data snapshots, not active source. |

## Protected items

The update intentionally preserves:

- `Data/`
- `ImportedDatabases/`
- `Saves/`
- `.git/`
- `Apps/`
- `Core/`
- `Tools/`
- `Updates/`
- `Docs/`
