# MMExplorer Rooms Decoder 0138

This bundle is generated directly from `ImportedDatabases/data-v1.11n.mdb`, owner table page `1664`.

The Access table definition identifies these columns:

`Map Number`, `Room Number`, `Name`, `Light`, `Shop`, `NPC`, `CMD`, `Spell`, `Lair`, `Delay`, `Placed`, `N`, `S`, `E`, `W`, `NE`, `NW`, `SE`, `SW`, `U`, `D`.

Generated files:

- `rooms_decoded.json`: 26,666 map-qualified room records.
- `exits_decoded.json`: 61,976 directional exit records.
- `room_interactions_decoded.json`: 284 action-command interaction records stored in direction columns.
- `decode_errors.json`: rows that could not be decoded. The current result is empty.
- `decode_manifest.json`: source hash, schema, counts, and decoder policy.

The decoder structures explicit MMExplorer qualifiers including doors and their picklocks/strength values, required item IDs, tolls, damage traps, spell traps, cast transitions, hidden/searchable exits, action counts/order, and text commands. Unknown qualifiers remain visible and evidence-preserved.

Run from the repository root:

```bash
python Tools/MDBExtractor/decode_mmexplorer_rooms.py --fail-on-errors
python -m pytest -q Tools/MDBExtractor/tests/test_decode_mmexplorer_rooms.py
```

Runtime integration must retain map-qualified identity. Do not collapse `room.mapX.Y` records to only room number because room numbers repeat across maps.
