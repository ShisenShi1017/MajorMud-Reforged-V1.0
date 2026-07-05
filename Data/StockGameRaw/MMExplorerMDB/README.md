# MMExplorer MDB Stock Data Surface

CodeUpdate-0065 creates this raw stock data surface from `ImportedDatabases/data-v1.11n.mdb`.

The source MDB is treated as an approved canonical repair source where fields map directly to Reforged canonical data. It is not a blanket promotion source for undecoded byte fields.

## Generated Files

- `raw_data_rows.json` - raw printable row extraction from the MDB page scan.
- `mdb_page_inventory.json` - page inventory from the MDB scan.
- `owner_table_summary.json` / `.csv` - grouped owner-table summary used to identify major stock tables.
- `owner_table_rows.json` - selected owner-table rows exported for direct repair and follow-up decoding.
- `ability_rows.json` / `.csv` - legacy extractor compatibility output; empty for this MDB pass.

## Owner Table Findings

- Owner table `1664`: room/world row surface.
- Owner table `322`: monster/NPC row surface candidate.
- Owner table `2566`: shop row surface candidate.
- Owner table `15`: class row surface candidate.
- Owner table `1660`: race row surface candidate.

## CodeUpdate-0065 Direct Repairs

- Added canonical room `wcc_map1_room2150` from MDB map `1`, room `2150`, name `Newhaven, Arena`.
- Added canonical room `wcc_map1_room2156` from MDB map `1`, room `2156`, name `Small Cavern`.
- Added both room ids to `Data/world.json`.
- Raised canonical/world room count from `1246` to `1248`.

Exit target references were preserved from printable MDB row segments. Exit type semantics remain unknown and are marked with `exit_type: -1` plus `exitTypeKnown: false`.
