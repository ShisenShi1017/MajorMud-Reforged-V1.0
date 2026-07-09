# CodeUpdate 0116 - Librarium Runtime Conversion and Repository Cleanup

## Summary

CodeUpdate 0116 aligns the repository with the agreed architecture:

```text
Librarium = authoritative source of truth
/Data = generated runtime output per world
```

## Completed

- Added machine-readable Librarium runtime definition scaffold.
- Added Classic, Custom template, and AI template world definition records.
- Added host-editable world override locations under `Librarium/WorldOverrides`.
- Added `Tools/Librarium/export_librarium_to_data.py` to generate `/Data/<World>` packages from the Librarium.
- Removed committed/generated `/Data`, `/Generated`, and `/DataDecodeCheckpoints` clutter from the cleaned repo.
- Removed old `/ImportedDatabases` snapshots and replaced them with canonical GitHub evidence source imports under `Evidence/CanonicalSources`.
- Archived old root CodeUpdate handoff/changelog files into `Documentation/Archive/CodeUpdateHistory` to reduce root clutter.
- Updated `.gitignore` so `/Data` remains generated and uncommitted.
- Added export validation test.

## Not changed

- Runtime gameplay logic was not rewritten in this pass.
- Formula conflicts remain documented for later resolution.
- Server consumption of generated world data is the next runtime milestone.
