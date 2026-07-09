# CodeUpdate 0128 Handoff - SQL/MDB Data to Librarium

Use this repo as the next baseline. The SQL/MDB data is now represented in the Librarium but should be treated as source/evidence-backed object material, not direct runtime data.

Next work:
1. Continue field-level byte decoding for SQL/MDB owner tables.
2. Promote decoded fields into canonical object properties only when proven.
3. Keep `/Data` generated from Librarium.
4. Keep `ImportedDatabases` until Librarium is declared 100% complete.

Metrics:
- imported records: 31473
- relationships: 66296
- raw rows inventoried: 37138
