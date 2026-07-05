# CodeUpdate-0065 - MMExplorer MDB Canonical Repair

CodeUpdate-0065 repairs canonical room data directly from the MMExplorer MDB already present in `ImportedDatabases/data-v1.11n.mdb`.

## Source

- Source file: `ImportedDatabases/data-v1.11n.mdb`
- Extracted surface: `Data/StockGameRaw/MMExplorerMDB/`
- Extraction tool: `Tools/MDBExtractor/offline_mdb_text_extractor.py`
- Promotion tool: `Tools/MDBExtractor/promote_mdb_rooms.py`

## Repair Applied

- Added `wcc_map1_room2150` as `Newhaven, Arena`.
- Added `wcc_map1_room2156` as `Small Cavern`.
- Added both rooms to `Data/world.json`.
- Updated smoke-test expectations from `1246` to `1248` canonical/world rooms.

The name `Small Cavern` for room `1/2156` comes from the MDB row itself. Older route or checkpoint labels that describe the room differently are not promoted over the MDB row.

## Scope Boundary

This update promotes only direct MDB-backed fields:

- map id
- room number
- room name
- raw printable exit references

It does not promote undecoded byte fields, formulas, runtime timing, combat behavior, AI, scripts, networking, command execution, scheduler execution, or gameplay systems.

## Follow-Up

Use the same MDB extraction and promotion path for the remaining canonical repairs:

- monster/NPC rows from owner table `322`
- shop rows from owner table `2566`
- class rows from owner table `15`
- race rows from owner table `1660`
- additional room fields from owner table `1664` after byte-level field positions are decoded
