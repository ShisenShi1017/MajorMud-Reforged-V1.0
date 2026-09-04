# Serialization Contract

Core serialization will use simple, human-editable JSON for canonical `Data/` records.

Rules:

- Preserve unknown fields.
- Preserve legacy IDs.
- Add stable internal IDs where useful.
- Do not require original `.DAT`, `.MDB`, `.ACCDB`, or companion client `.mp` files at server runtime.
- Write validation reports when converting or loading data.
