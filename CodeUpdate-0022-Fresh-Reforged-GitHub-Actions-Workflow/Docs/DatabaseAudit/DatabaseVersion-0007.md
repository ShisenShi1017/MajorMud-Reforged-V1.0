# Database Version 0007 DAT Decode Continuation Report

## Summary
This database version continues native `.DAT` decoding with stricter terminology. Numeric values are not labeled as record counts, skill IDs, flags, item IDs, or references unless cross-check evidence supports that label.

## Source File Catalog
- `WCCACMSR.DAT`: 6,144 bytes, SHA-256 `ce9790c179034788...`, 0 printable string runs
- `WCCACTS.DAT`: 65,536 bytes, SHA-256 `baf14617411fd3e4...`, 1,309 printable string runs
- `WCCBANKS.DAT`: 1,536 bytes, SHA-256 `58b3fa431854beb8...`, 0 printable string runs
- `WCCCLASS.DAT`: 6,144 bytes, SHA-256 `8cc91e569b45c8b2...`, 30 printable string runs
- `WCCGANGS.DAT`: 2,048 bytes, SHA-256 `19ab7942a50ede67...`, 0 printable string runs
- `WCCITEMS.DAT`: 1,404,928 bytes, SHA-256 `22d54b7aab28e1db...`, 11,627 printable string runs
- `WCCKNMSR.DAT`: 561,152 bytes, SHA-256 `b33f96d837052006...`, 5,656 printable string runs
- `WCCMP001.DAT`: 30,973,440 bytes, SHA-256 `17442fe74d608511...`, 169,130 printable string runs
- `WCCMSG.DAT`: 870,912 bytes, SHA-256 `37546e9bdc91ffb0...`, 16,363 printable string runs
- `WCCRACE.DAT`: 3,072 bytes, SHA-256 `1c227140da2fa8e6...`, 25 printable string runs
- `WCCSHOPS.DAT`: 78,336 bytes, SHA-256 `1d5d8bd676717b1f...`, 762 printable string runs
- `WCCSPELS.DAT`: 256,512 bytes, SHA-256 `131d9221f12e4ff0...`, 5,425 printable string runs
- `WCCTEXT.DAT`: 1,073,152 bytes, SHA-256 `de1232049704ccbe...`, 25,721 printable string runs
- `WCCUPDAT.DAT`: 58,357,760 bytes, SHA-256 `f7e4cf0a261ff381...`, 319,563 printable string runs
- `WCCUSERS.DAT`: 10,240 bytes, SHA-256 `88d6a7002a20ed1b...`, 0 printable string runs

## Stronger Findings
- `WCCSHOPS.DAT`: 512-byte shop slot hypothesis remains the strongest current finding. CSV shop names match consistently near slot offset +10 for many records.
- `WCCCLASS.DAT`: 153-byte class slot hypothesis remains plausible for the 15 CSV classes, but shifted bytes require caution before field naming.
- `WCCITEMS.DAT`: 1024-byte indexed item blocks are plausible for many CSV item names. Some items are absent or may be represented differently; no full field decode claimed.
- `WCCSPELS.DAT`: 256-byte indexed spell blocks are plausible for many spell names, but name offsets alternate and may include message/action records; no full field decode claimed.
- `WCCRACE.DAT`: race names are present, but ordering/slot mapping is not fully proven; do not assume all numeric columns are field positions yet.

## Correction Rule Reaffirmed
Numbers in binary records may represent skills, abilities, flags, lookup codes, text pointers, offsets, room IDs, shop indexes, spell IDs, item IDs, monster IDs, prices, or counts. Until cross-referenced, they are documented as `unknown_numeric_at_offset_*`.

## Files Added
- `Data/NativeDatDecode/Reports/file_catalog_v0007.json`
- `Data/NativeDatDecode/Reports/csv_name_match_v0007.json`
- `Data/NativeDatDecode/Reports/fixed_slot_hypotheses_v0007.json`
- `Tools/Importer/dat_deep_probe_v0007.py`

## Next Decode Targets
1. Generate byte-level overlays for shop records because the 512-byte slot structure is strongest.
2. Map each shop inventory field against CSV `Item-#`, `Max-#`, `Time-#`, `Amount-#`, and `%#` columns.
3. Use the confirmed shop pattern to establish repeatable evidence rules before mapping items/spells/classes.
