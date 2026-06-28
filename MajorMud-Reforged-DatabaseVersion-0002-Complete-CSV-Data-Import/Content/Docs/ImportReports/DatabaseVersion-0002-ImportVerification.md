# Database Version 0002 Import Verification Report

This report covers the user-provided CSV export archive `E.zip`.

## Result

Database Version 0002 converts every provided CSV table into editable JSON under `Data/`.

No duplicate primary keys were found in any converted table.

## Converted Tables

| Data Folder | Source CSV | Records | Columns | Duplicate Primary Keys |
|---|---:|---:|---:|---:|
| Classes | classes.TXT | 15 | 30 | 0 |
| Items | items.TXT | 2017 | 95 | 0 |
| Lairs | lairs.TXT | 421 | 15 | 0 |
| Monsters | monsters.TXT | 1126 | 134 | 0 |
| Races | races.TXT | 13 | 37 | 0 |
| Rooms | rooms.TXT | 26666 | 21 | 0 |
| Shops | shops.TXT | 178 | 109 | 0 |
| Spells | spells.TXT | 1458 | 46 | 0 |
| TextBlocks | tbinfo.TXT | 2279 | 4 | 0 |

## Important Scope Note

This database version verifies and converts every table provided in the CSV archive. The original Access/MDB binary is no longer required for these converted tables during normal server operation. If additional tables are later discovered outside this CSV archive, they must be added in a future database version and documented here.

## Standalone Rule

After this database version is applied, MajorMud Reforged should treat `Data/` as the working game database. `ImportedDatabases/` remains reference-only and must not be overwritten by database versions.
