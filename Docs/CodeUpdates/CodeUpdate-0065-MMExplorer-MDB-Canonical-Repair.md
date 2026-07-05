# CodeUpdate-0065 - MMExplorer MDB Canonical Repair

## Summary

CodeUpdate-0065 repairs canonical room/world data from the MMExplorer MDB already preserved at `ImportedDatabases/data-v1.11n.mdb`.

This update treats the MDB as an approved repair source for directly mapped canonical fields. It does not use the MDB as a blanket override for undecoded byte fields, formulas, timing, or runtime behavior.

## Changed

- Extended `Tools/MDBExtractor/offline_mdb_text_extractor.py` with owner-table summary and selected owner-row export support.
- Added `Data/StockGameRaw/MMExplorerMDB/` as the current raw stock data surface.
- Added `Tools/MDBExtractor/promote_mdb_rooms.py` for reproducible direct room promotion.
- Added canonical room `wcc_map1_room2150` as `Newhaven, Arena`.
- Added canonical room `wcc_map1_room2156` as `Small Cavern`.
- Added both room ids to `Data/world.json`.
- Updated smoke-test expectations from `1246` to `1248` canonical/world rooms.

## Direct Fields Promoted

- map id
- room number
- room name
- raw printable exit references

Unknown exit semantics are represented as `exit_type: -1` with `exitTypeKnown: false`.

## Owner Table Findings

- `1664`: room/world row surface.
- `322`: monster/NPC row surface candidate.
- `2566`: shop row surface candidate.
- `15`: class row surface candidate.
- `1660`: race row surface candidate.

## Boundaries

- No gameplay system.
- No command execution.
- No scheduler execution.
- No sockets or sessions.
- No movement execution.
- No combat execution.
- No AI.
- No live editor behavior.
- No runtime formula/timing promotion.
- No imported/reference deletion.
- No unknown byte-field promotion.

## Verification

Local verification is recorded in `UpdateManifest.json` after the final validation run.

GitHub Actions remains the official gate after the user pushes this update and reports the result.
