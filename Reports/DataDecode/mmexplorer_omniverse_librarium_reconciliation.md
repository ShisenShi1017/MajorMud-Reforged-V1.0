# CodeUpdate0139 MMExplorer and OmniVerse Librarium Reconciliation

This report records structured reference promotion into Librarium.

- MMExplorer SQL/MDB rows are promoted as matched reference blocks or unmatched supplement records.
- OmniVerse codex rows are promoted as public reference blocks and do not overwrite binary-backed fields.
- Ability records are added as a new Librarium family from OmniVerse public codex data.

## OmniVerse Promotions

| Family | Matched | Unmatched | Source Records |
| --- | ---: | ---: | ---: |
| items | 1950 | 0 | 1950 |
| spells | 1350 | 0 | 1350 |
| npcs | 1100 | 0 | 1100 |
| shops | 178 | 0 | 178 |

## MMExplorer SQL/MDB Promotions

| Family | Matched | Unmatched supplements | Source Records |
| --- | ---: | ---: | ---: |
| items | 1950 | 67 | 2017 |
| spells | 1350 | 108 | 1458 |
| npcs | 1100 | 26 | 1126 |
| shops | 178 | 0 | 178 |

- Character foundation race matches: 13.
- Character foundation class matches: 15.
- Ability records created: 188.

Raw details: `Reports/DataDecode/mmexplorer_omniverse_librarium_reconciliation_0139.json`
