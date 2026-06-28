# MajorMud Reforged Version State

| Item | Value |
|---|---|
| Project milestone | Repository Reorganization & Core Foundation |
| Current code update | Code Update 0001 |
| Current database model | Reforged `Data/` is authoritative |
| Imported databases | Reference/fallback only |
| Runtime dependency target | Standalone; no Access/OLEDB; no legacy `.DAT` required at runtime |
| PowerShell status | Prototype/legacy only |
| Executable status | Dedicated folders created; no executable claimed yet |

## Next planned update

**Code Update 0002 — Core Data Loader & Server Bootstrap**

Goals:
- Add real Core data models.
- Load `Data/` into memory.
- Validate database structure.
- Generate validation logs.
- Start and stop server bootstrap cleanly.
