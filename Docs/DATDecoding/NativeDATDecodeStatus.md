# Native DAT Decode Status

This document tracks the native MajorMud `.DAT` decoding effort.

## Standing Rule

Do not mark a native DAT file as fully decoded until:

- every record is identified,
- every known field is named,
- unknown fields/bytes are preserved,
- source and converted record counts match,
- duplicates are explained,
- and the result is verified against CSV/Access/runtime evidence.

## Current Format Notes

### WCCSHOPS.DAT

Current confidence: **high**

- Physical block size: `512` bytes
- Visible shop number appears at offset `+6`
- Shop name begins at offset `+10`
- CSV crosscheck found 140 shop names.

### WCCSPELS.DAT

Current confidence: **medium**

- Physical block size: `256` bytes
- Spell number appears at offset `+6` for identified records
- Spell name begins at offset `+8` for identified records
- Many CSV spells are not in this base DAT and may live in update data.

### WCCITEMS.DAT

Current confidence: **medium**

- Strong 1024-byte block pattern.
- Item name/description strings appear near the beginning of records.
- Some item names are duplicated in CSV or differ from native spelling.
- ID mapping is not final.

### WCCCLASS.DAT

Current confidence: **partial**

- All 15 CSV class names were found.
- Names are anchored, but the record stride is not fully finalized.

### WCCRACE.DAT

Current confidence: **partial**

- All 13 CSV race names were found.
- Names are anchored, but the record stride is not fully finalized.

### Large Files

`WCCMP001.DAT` and `WCCUPDAT.DAT` are cataloged but not decoded. They are likely essential for world/update data and must be decoded after smaller table files are proven.

