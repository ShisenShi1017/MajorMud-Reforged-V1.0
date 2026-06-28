# Database Version 0006 - DAT Decode Correction and Continuation

## Summary
This database version continues native Reforged `.DAT` decoding and corrects prior terminology around numeric fields.

## Key correction
Numeric values found inside records must not be treated as record counts unless proven by file headers or verified table structure. Many Reforged values are skill IDs, ability references, flags, type codes, item references, room references, offsets, or bitfields.

## Added
- `Docs/DatabaseAudit/DatabaseVersion-0006-DAT-Decode-Correction.md`
- `Docs/DatabaseAudit/dat_file_catalog.json`
- `Docs/DatabaseAudit/csv_dat_name_alignment.json`
- `Docs/DatabaseAudit/column_semantics_warnings.json`
- `Tools/Importer/Analyze-DatFiles.py`

## Decode progress
- Identified stronger block-size candidates for several `.DAT` files.
- Matched CSV names against native `.DAT` files where possible.
- Confirmed that classes, races, shops, spells, and items have partially inferable record structure.
- Left monsters, rooms, lairs, and text block linkage marked as unresolved until binary linkage is proven.

## Safety
- Does not modify `Saves/`.
- Does not modify `ImportedDatabases/`.
- Does not overwrite runtime data.
- Does not claim full `.DAT` decoding is complete.
