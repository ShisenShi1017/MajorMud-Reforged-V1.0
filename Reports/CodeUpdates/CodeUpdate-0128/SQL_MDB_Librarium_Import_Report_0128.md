# CodeUpdate 0128 - SQL/MDB Data Added to Librarium

## Result

Added the MMExplorer SQL-era MDB source data to the Librarium as structured source records.

- Source MDB: `ImportedDatabases/data-v1.11n.mdb`
- SDK schema referenced: `Tools/LibrariumDistribution/sdk_schema.sql`
- MDB pages inventoried: 2885
- Raw MDB rows inventoried: 37138
- Selected Librarium records imported: 31473
- Relationships captured from printable source references: 66296

## Family counts

| Family | Records |
|---|---:|
| rooms | 26666 |
| items | 2017 |
| npcs | 1126 |
| spells | 1458 |
| shops | 178 |
| classes | 15 |
| races | 13 |

## Architecture rule preserved

The Librarium remains the authoritative source. The SQL/MDB import enriches Librarium knowledge and evidence. `/Data` remains generated runtime output and should not be manually edited.

## Confidence

This pass is high confidence for row identity, names, printable relationships, source locations, and raw row provenance. Byte-level fields that are not yet decoded remain marked as requiring continued field-level reconstruction before classic-verified promotion.

## Runtime

Runtime gameplay code was not changed.
