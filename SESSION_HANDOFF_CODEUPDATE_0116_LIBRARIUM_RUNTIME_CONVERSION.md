# Session Handoff - CodeUpdate 0116

## Baseline

Started from `MajorMud-Reforged-V1.0-main(23).zip` plus latest Phase 2 Librarium population update.

## Decision locked

The Librarium is the source of truth. `/Data` is generated runtime output per world.

## What changed

- Added Librarium RuntimeDefinitions scaffold.
- Added world definition records for Classic, Custom template, and AI template.
- Added export tool to generate `/Data/Classic`.
- Cleaned legacy generated folders and old import database clutter.
- Replaced old imported database snapshots with canonical GitHub evidence sources.
- Updated documentation for CodeUpdate 0116.

## Next recommended Codex task

Update server/runtime loading to consume generated `/Data/<World>` packages produced by `Tools/Librarium/export_librarium_to_data.py`, while keeping host edits pointed at the Librarium.

## Important commands

```bash
python Tools/Librarium/export_librarium_to_data.py --world classic --clean
python -m pytest Tests/test_librarium_export_0116.py
```
