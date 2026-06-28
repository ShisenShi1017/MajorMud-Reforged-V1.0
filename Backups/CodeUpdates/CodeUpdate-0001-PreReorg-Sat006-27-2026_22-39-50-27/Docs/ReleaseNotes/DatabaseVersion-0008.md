# Database Version 0008 - MegaMud MP Knowledge Integration

This database version integrates the uploaded MegaMud 2.0 `.mp` map files as a new evidence source for MajorMud Reforged.

## Verified inputs

- MegaMud `.mp` files processed: **1957**
- CSV room records available for cross-reference: **26666**
- MP room references found: **2919**
- Unique MP map/room references: **648**
- MP room references matched to `rooms.TXT`: **2911**
- Exact normalized name matches: **803**
- Unique human-readable route commands found: **430**

## Important correction

The `.mp` route-line numeric fields are **not** being treated as counts, IDs, skills, or flags yet. They remain unknown binary/map-route fields until proven by cross-reference with native `.DAT` files, CSV exports, and runtime behavior.

## What this adds

- `ImportedDatabases/MegaMudMP/` original `.mp` reference archive.
- `Data/MegaMudMaps/Records/` one JSON evidence file per `.mp` source file.
- `Data/MegaMudMaps/Index/summary.json` conversion summary.
- `Data/MegaMudMaps/Index/roomReferenceIndex.json` map/room to `.mp` file index.
- `Data/MegaMudMaps/Index/duplicateRoomReferences.json` repeated room references across MP files.
- `Data/MegaMudMaps/Index/humanCommands.json` extracted human-readable route commands.
- `Tools/Importer/parse_megamud_mp.py` repeatable parser.

## Standalone rule

These files are evidence for building Reforged `Data/`. The future server should still load from Reforged `Data/`, not from MegaMud files directly.
