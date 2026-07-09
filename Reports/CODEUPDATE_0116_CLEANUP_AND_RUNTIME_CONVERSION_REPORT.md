# CodeUpdate 0116 Cleanup and Runtime Conversion Report

## Repo cleanup

Removed generated or obsolete source-of-truth folders from the committed repo image:

- `/Data` - generated runtime output only.
- `/Generated` - local generated artifacts only.
- `/DataDecodeCheckpoints` - historical generated checkpoints.
- `/ImportedDatabases` - replaced by canonical GitHub evidence source imports.

Archived older root CodeUpdate files to `Documentation/Archive/CodeUpdateHistory`.

## New canonical evidence location

`Evidence/CanonicalSources` now contains the four canonical source trees used for evidence mining:

- MBBSEmu
- MMUD Explorer
- Nightmare Redux
- MajorMUD PHP Monster Simulator

## Librarium runtime conversion

Machine-readable runtime definitions now live in `Librarium/RuntimeDefinitions`. The export tool produces `/Data/<World>` packages from these definitions.

## Validation

Run:

```bash
python Tools/Librarium/export_librarium_to_data.py --world classic --clean
python -m pytest Tests/test_librarium_export_0116.py
```
