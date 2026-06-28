# Suggestions

## Implemented in Code Update 0004

- Add Core as the single shared foundation.
- Add project versioning separate from database and code update versions.
- Add repository health report per update.
- Keep executable folders separated under `Bin/Windows/`.

## Implemented in Code Update 0016

- Core Data Loader
  - Reads canonical `Data/` JSON.
  - Produces repository-backed Core records.
  - Preserves unknown/raw fields for future editor and server use.
- Build Verification Without Local SDK
  - Adds GitHub Actions cloud build verification.
  - Lets GitHub install/use .NET SDK in the cloud instead of requiring the user to install it locally.

## Next Suggestions To Implement

1. Core Validation Service
   - Checks duplicate IDs.
   - Checks missing references.
   - Produces reports under `Reports/`.

2. Server Bootstrap v2
   - Uses Core instead of standalone/prototype loading logic.
