# Database Version 0006 DAT Decode Correction Report

This report corrects earlier wording: numeric values found inside records are not automatically record counts. Many are skill IDs, ability IDs, flags, type codes, references, offsets, or bitfields.

## Source DAT catalog

| File | Size | Strings >=4 | Even block candidates |
|---|---:|---:|---|
| WCCACMSR.DAT | 6,144 | 0 | 256x24, 384x16, 512x12, 768x8, 1024x6, 2048x3 |
| WCCACTS.DAT | 65,536 | 1,309 | 2048x32, 4096x16, 8192x8, 16384x4, 32768x2, 65536x1 |
| WCCBANKS.DAT | 1,536 | 0 | 128x12, 192x8, 256x6, 384x4, 512x3, 768x2 |
| WCCCLASS.DAT | 6,144 | 30 | 256x24, 384x16, 512x12, 768x8, 1024x6, 2048x3 |
| WCCGANGS.DAT | 2,048 | 0 | 64x32, 128x16, 256x8, 512x4, 1024x2, 2048x1 |
| WCCITEMS.DAT | 1,404,928 | 11,627 | 128x10976, 256x5488, 512x2744, 1024x1372, 2048x686, 4096x343 |
| WCCKNMSR.DAT | 561,152 | 5,656 | 128x4384, 256x2192, 512x1096, 1024x548, 2048x274, 4096x137 |
| WCCMP001.DAT | 30,973,440 | 169,130 | 128x241980, 192x161320, 256x120990, 384x80660, 512x60495, 768x40330 |
| WCCMSG.DAT | 870,912 | 16,363 | 128x6804, 192x4536, 256x3402, 384x2268, 512x1701, 768x1134 |
| WCCRACE.DAT | 3,072 | 25 | 192x16, 256x12, 384x8, 512x6, 768x4, 1024x3 |
| WCCSHOPS.DAT | 78,336 | 762 | 153x512, 192x408, 256x306, 384x204, 512x153, 768x102 |
| WCCSPELS.DAT | 256,512 | 5,425 | 128x2004, 192x1336, 256x1002, 384x668, 512x501, 768x334 |
| WCCTEXT.DAT | 1,073,152 | 25,721 | 256x4192, 512x2096, 1024x1048, 2048x524, 4096x262, 8192x131 |
| WCCUPDAT.DAT | 58,357,760 | 319,563 | 64x911840, 128x455920, 256x227960, 512x113980, 1024x56990, 2048x28495 |
| WCCUSERS.DAT | 10,240 | 0 | 64x160, 128x80, 256x40, 512x20, 1024x10, 2048x5 |

## CSV to DAT name alignment

| CSV | DAT | CSV rows | Name matches | Working record-size assumption | Status |
|---|---|---:|---:|---|---|
| classes.TXT | WCCCLASS.DAT | 15 | 15 | 153 | partial structural decode only |
| races.TXT | WCCRACE.DAT | 13 | 13 | 126 | partial structural decode only |
| shops.TXT | WCCSHOPS.DAT | 178 | 140 | 512 | partial structural decode only |
| spells.TXT | WCCSPELS.DAT | 1,458 | 963 | 256 | partial structural decode only |
| items.TXT | WCCITEMS.DAT | 2,017 | 1330 | 1024 | partial structural decode only |
| monsters.TXT | TBD | 1,126 |  | TBD | no direct DAT matched yet |
| rooms.TXT | TBD | 26,666 |  | TBD | no direct DAT matched yet |
| lairs.TXT | TBD | 421 |  | TBD | no direct DAT matched yet |
| tbinfo.TXT | TBD | 2,279 |  | TBD | no direct DAT matched yet |

## Current verified decode facts

- `WCCSHOPS.DAT` strongly aligns to 512-byte slots for named shop records.
- `WCCSPELS.DAT` strongly aligns to 256-byte slots, but exact spell numbering/index offset still needs confirmation.
- `WCCITEMS.DAT` strongly uses 1024-byte item-like blocks for at least the base item table, but item numbers are offset from raw slots and not every CSV item name appears literally in the DAT.
- `WCCCLASS.DAT` class names advance in 153-byte record steps after an apparent header/padding area.
- `WCCRACE.DAT` race names advance in 126-byte record steps after an apparent header/padding area.
- Large files `WCCMP001.DAT` and `WCCUPDAT.DAT` are block based, but not decoded enough to label rooms/monsters/updates with certainty.

## Important correction

Earlier numeric observations must be treated as hypotheses unless tied to a CSV column and record layout. For example, values in `Abil-*`, `AttType-*`, `WeaponType`, `ArmourType`, `MageryType`, `Targets`, `ClassRest`, room exits, shop item slots, and similar columns are likely lookup IDs, skill/ability references, flags, type codes, or foreign keys. They are not record totals unless a separate count/header proves it.

## Next decode targets

1. Fully map `WCCCLASS.DAT` and `WCCRACE.DAT`, because their small records and CSV alignment make them safest.
2. Map `WCCSHOPS.DAT` inventory slot layout against `shops.TXT`.
3. Map `WCCSPELS.DAT` against `spells.TXT`, explicitly labeling ability/effect fields as references until proven otherwise.
4. Map `WCCITEMS.DAT` in 1024-byte blocks and preserve unknown bytes for every item-like slot.
5. Investigate where monster and room data live across `WCCMP001.DAT`, `WCCUPDAT.DAT`, `WCCTEXT.DAT`, and `WCCMSG.DAT`.