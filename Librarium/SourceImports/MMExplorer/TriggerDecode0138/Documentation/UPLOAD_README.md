# MMExplorer Movement Trigger Librarium Upload

## Proven contents

- Original `data-v1.11n.mdb` MMExplorer database.
- 26,666 raw rows associated with the MMExplorer room table (`owner_table_page = 1664`).
- Raw row offsets, lengths, hexadecimal prefixes/suffixes, and printable segments.
- 328 current movement/exit trigger identities with source room, direction, destination, observed exit type, clues, and evidence result.
- Door annotations preserved in printable MMExplorer room-row segments where present, such as `1/1381 (Door)`.

## Important boundary

The source database and raw rows are present, but this repository does not contain a proven table-column decoder that maps the remaining binary fields to named prerequisites such as:

- required item or key ID
- minimum or maximum level
- required flag and flag value
- required spell or ability ID
- class/race/alignment restriction

Those values must not be inferred from DataSim hypothesis labels. The normalized trigger index marks these records `prerequisite_status = not_decoded` and preserves them as `evidence_only_gate` records.

## Recommended Librarium ingestion

1. Upload `Source/data-v1.11n.mdb` as the authoritative imported database artifact.
2. Upload `Source/mmexplorer_room_table_raw_rows.json` under the MMExplorer source-import area.
3. Upload `Normalized/mmexplorer_movement_trigger_index.json` as the canonical staging index for all 328 triggers.
4. Preserve invalid destinations as hard-invalid relationships.
5. Preserve evidence-only gates as visible runtime records without fabricated prerequisites.
6. Add decoded prerequisite fields only after a table-specific MDB decoder proves their byte offsets and semantics.

## Suggested canonical fields

- `id`
- `from_room`
- `direction`
- `to_room`
- `exit_types`
- `evidence_results`
- `clues`
- `runtime_disposition`
- `prerequisite_status`
- `source_priority`
