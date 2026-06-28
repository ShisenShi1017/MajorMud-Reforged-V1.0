# Data Loading Contract

The server must load game content from `Data/` only.

Normal server startup must not read from:
- `ImportedDatabases/`
- original `.DAT` files
- `.mdb` or `.accdb` files
- CSV exports
- MegaMud `.mp` files

Those sources are import/reference inputs only.

## Required startup flow

1. Locate project root.
2. Load `Data/DataManifest.json` if present.
3. Load canonical data collections.
4. Validate duplicates and required references.
5. Build in-memory world model.
6. Write validation report to `Logs/`.
7. Start server only if required validation passes.

## Unknown fields

Unknown imported fields must be preserved until decoded. Do not discard data just because its meaning is not known yet.
