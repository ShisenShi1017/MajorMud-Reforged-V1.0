# Database Version 0007 - DAT Decode Field Mapping Continuation

## Type
Database Version / Native DAT decoding continuation.

## What Changed
- Continued native `.DAT` analysis with stricter evidence labels.
- Added file catalog with SHA-256 hashes and printable string counts.
- Added CSV-name-to-DAT-offset match report.
- Added fixed-slot hypothesis report.
- Added improved probe tool under `Tools/Importer`.
- Updated documentation to avoid mislabeling skill/reference numbers as counts.

## Safety
- Runs from its own extracted DatabaseVersion-0007 folder.
- Does not overwrite `Saves/`.
- Does not modify `ImportedDatabases/`.
- Does not claim full DAT decoding is complete.
