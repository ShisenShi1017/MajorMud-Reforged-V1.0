# CHANGELOG - CodeUpdate 0120 Baseline Correction

## Status
PASS_BASELINE_CORRECTION

## Fixed

- Corrected CodeUpdate 0119 metrics from "941 text files scanned" to:
  - 941 total canonical source files classified
  - 657 text files scanned
  - 284 binary/asset files classified
- Added authoritative metrics files in `Reports/CodeUpdate0120/` and `Librarium/Reports/`.
- Added `SOURCE_OF_TRUTH.md`.
- Added `REPOSITORY_RULES.md`.
- Updated root `README.md` to CodeUpdate 0120.
- Preserved `/ImportedDatabases`.
- Preserved `/Data` as generated-output placeholder.

## Runtime Impact

Runtime gameplay code was not modified.

## Next

Step 4: build the Librarium-to-Data generator/compiler against the corrected baseline.
