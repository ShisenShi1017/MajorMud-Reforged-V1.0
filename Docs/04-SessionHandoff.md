# Session Handoff

## Current Focus

Native MajorMud `.DAT` decoding for standalone Reforged data.

## Last Completed

Database Version 0004 - Native DAT Decode Foundation.

## Current State

- CSV-converted data exists from Database Version 0002.
- Native `.DAT` runtime files have been cataloged and structurally inspected.
- Full binary decoding is not complete yet.
- The next best step is to fully map small stable files first: races, classes, then shops.

## Important Rules

- Data/database work is released as Database Versions.
- Code/server/editor/client work is released as Patches.
- Every Database Version or Patch must run from its own extracted folder.
- Every Database Version or Patch must update docs.
- Imported source files are reference/fallback only after conversion.
- The standalone server must ultimately load from `Data/`, not from original `.DAT`, `.MDB`, `.ACCDB`, or CSV files.

## Next Recommended Task

Database Version 0005: fully decode `WCCRACE.DAT` and `WCCCLASS.DAT`, prove every race/class record against CSV, and preserve all unknown bytes.
