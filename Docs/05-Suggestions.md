# Suggestions

## Implemented in Code Update 0004

- Add Core as the single shared foundation.
- Add project versioning separate from database and code update versions.
- Add repository health report per update.
- Keep executable folders separated under `Bin/Windows/`.

## Next Suggestions To Implement

1. Core Data Loader
   - Reads canonical `Data/` JSON.
   - Produces typed world records.
   - Preserves unknown fields.

2. Core Validation Service
   - Checks duplicate IDs.
   - Checks missing references.
   - Produces reports under `Reports/`.

3. Server Bootstrap v2
   - Uses Core instead of standalone/prototype loading logic.
