# Database Version 0008 - companion client MP Knowledge Integration

This database version integrates the uploaded companion client 2.0 `.mp` map files as a new evidence source for Reforged.

## Verified inputs

- companion client `.mp` files processed: **1957**
- CSV room records available for cross-reference: **26666**
- MP room references found: **2919**
- Unique MP map/room references: **648**
- MP room references matched to `rooms.TXT`: **2911**
- Exact normalized name matches: **803**
- Unique human-readable route commands found: **430**

## Important correction

The `.mp` route-line numeric fields are **not** being treated as counts, IDs, skills, or flags yet. They remain unknown binary/map-route fields until proven by cross-reference with native `.DAT` files, CSV exports, and runtime behavior.

## What this adds

- `ImportedDatabases/companion clientMP/` original `.mp` reference archive.
- `Data/companion clientMaps/Records/` one JSON evidence file per `.mp` source file.
- `Data/companion clientMaps/Index/summary.json` conversion summary.
- `Data/companion clientMaps/Index/roomReferenceIndex.json` map/room to `.mp` file index.
- `Data/companion clientMaps/Index/duplicateRoomReferences.json` repeated room references across MP files.
- `Data/companion clientMaps/Index/humanCommands.json` extracted human-readable route commands.
- `Tools/Importer/parse_megamud_mp.py` repeatable parser.

## Standalone rule

These files are evidence for building Reforged `Data/`. The future server should still load from Reforged `Data/`, not from companion client files directly.
