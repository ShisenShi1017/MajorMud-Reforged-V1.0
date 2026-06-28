# Database Version 0005 - Native DAT Decode Continuation

## Status

Database Version 0005 continues native `.DAT` decoding. It is a decoder/tooling and audit update, not a final full conversion.

The project rule remains: full `.DAT` decoding is not complete until every source file, record, field, and unknown byte is accounted for and verified.

## Source Files Cataloged

| File | Bytes | Current decode status |
|---|---:|---|
| WCCACMSR.DAT | 6,144 | Cataloged; binary/no printable strings found |
| WCCACTS.DAT | 65,536 | Cataloged; string extraction started |
| WCCBANKS.DAT | 1,536 | Cataloged; binary/no printable strings found |
| WCCCLASS.DAT | 6,144 | 15/15 CSV class names found; partial name-anchored decode |
| WCCGANGS.DAT | 2,048 | Cataloged; binary/no printable strings found |
| WCCITEMS.DAT | 1,404,928 | 1024-byte block evidence; 1,330 CSV item names found |
| WCCKNMSR.DAT | 561,152 | Cataloged; string extraction started |
| WCCMP001.DAT | 30,973,440 | Cataloged; large world/map/update candidate; string extraction started |
| WCCMSG.DAT | 870,912 | Cataloged; message strings extracted |
| WCCRACE.DAT | 3,072 | 13/13 CSV race names found; partial name-anchored decode |
| WCCSHOPS.DAT | 78,336 | Strong 512-byte shop block evidence; 140 CSV shop names found |
| WCCSPELS.DAT | 256,512 | 256-byte spell block evidence; 964 CSV spell names found |
| WCCTEXT.DAT | 1,073,152 | Cataloged; text strings extracted |
| WCCUPDAT.DAT | 58,357,760 | Cataloged; large update data candidate; string extraction started |
| WCCUSERS.DAT | 10,240 | Cataloged; binary/no printable strings found |

## New Decode Findings

### High-confidence findings

- `WCCSHOPS.DAT` aligns cleanly to 512-byte physical blocks.
- Shop records store the visible shop number at byte offset `+6` in little-endian form.
- Shop names start at byte offset `+10` within each 512-byte block.
- `WCCCLASS.DAT` contains all 15 class names from the CSV export.
- `WCCRACE.DAT` contains all 13 race names from the CSV export.

### Medium-confidence findings

- `WCCSPELS.DAT` aligns cleanly to 256-byte physical records for many spell entries.
- Spell IDs appear at byte offset `+6` and spell names usually begin at byte offset `+8` within identified records.
- `WCCITEMS.DAT` aligns strongly to 1024-byte item-like blocks.
- Many item records contain a name and a long description string near the start of the block.
- Several CSV items/spells not found in the base DAT are likely stored in update/expansion files such as `WCCUPDAT.DAT`, or differ by spelling/formatting.

### Not yet final

- `WCCUPDAT.DAT` and `WCCMP001.DAT` are very large and likely contain important map/update data. They are cataloged and string-scanned, but not fully decoded.
- Unknown numeric fields are not yet assigned final names.
- Duplicate visible names in the CSV create false duplicate-name matches for item/spell verification, so ID-level verification is still required.

## CSV Crosscheck Summary

| Native DAT | CSV table | CSV names found | CSV names missing | Duplicate name-offset cases |
|---|---|---:|---:|---:|
| WCCCLASS.DAT | classes.TXT | 15 | 0 | 0 |
| WCCRACE.DAT | races.TXT | 13 | 0 | 0 |
| WCCSHOPS.DAT | shops.TXT | 140 | 38 | 0 |
| WCCSPELS.DAT | spells.TXT | 964 | 492 | 131 |
| WCCITEMS.DAT | items.TXT | 1,330 | 608 | 64 |

## Files Added

- `Tools/Importer/native_dat_probe.py`
- `Data/NativeDatDecodeReports/native_dat_manifest.json`
- `Data/NativeDatDecodeReports/*.strings.json`
- `Data/NativeDatDecodeReports/*.records.json` for fixed-block candidates
- `Data/NativeDatDecodeReports/*.csv-crosscheck.json` for CSV-backed files
- `Docs/DATDecoding/*.md`

## Next Recommended Step

Decode in this order:

1. `WCCSHOPS.DAT` because the block layout is strongest.
2. `WCCCLASS.DAT` and `WCCRACE.DAT` because the CSV match is complete.
3. `WCCSPELS.DAT` to separate base spells from update/expansion spells.
4. `WCCITEMS.DAT` to separate base items from update/expansion items and duplicate visible names.
5. `WCCMP001.DAT` and `WCCUPDAT.DAT` after the small record formats are proven.

