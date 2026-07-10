# CodeUpdate 0132 — Runtime Room/Exit Load Slice

## Purpose
Prepare the server for the next Classic test by resolving the previous runtime symptom: local start showed `Unknown Room` with no exits even though generated-data validation passed.

## Changes
- Enriched `Data/Classic/rooms.json` with runtime-ready room identity fields:
  - `mapId` / `map_id`
  - `roomNumber` / `room_number`
  - evidence-backed `name`
  - embedded `exits`
- Rebuilt `Data/Classic/exits.json` from retained OmniVerse roommap evidence.
- Added `--smoke-room-exits` server validation command.
- Hardened `--smoke-generated-data` so it also checks start-room identity and exits.
- Added `RuntimeRoomExitSmokeReport` models.
- Added `Tools/Validation/validate_codeupdate0132_runtime_room_exit_slice.py`.

## Validation
- Static room/exit validation: PASS
- Start room: `room.1` / `Town Gates`
- Start exits: `N -> room.3`, `E -> room.1381`, `W -> room.101`
- Runtime gameplay systems beyond room movement were not changed.

## Preserved
- `ImportedDatabases/` retained.
- `Saves/` retained.
- Librarium remains source of truth.
- `/Data` remains generated runtime output.
