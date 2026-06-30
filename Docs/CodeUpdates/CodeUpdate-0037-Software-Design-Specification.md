# CodeUpdate-0037 - Software Design Specification and Continuity Cleanup

## Purpose

CodeUpdate-0037 combines the user-provided CodeUpdate-0036B continuity cleanup package with the user-provided CodeUpdate-0037 Software Design Specification package.

This update repairs stale continuity left after the direct-GitHub CodeUpdate-0036 documentation update and creates draft Software Design Specification records for future systems.

## Changed

- Synchronized continuity documents from CodeUpdate-0035/0036 state to CodeUpdate-0037.
- Recorded CodeUpdate-0036 as a direct-GitHub knowledge-base/design-decision documentation update.
- Folded CodeUpdate-0036B corrective continuity notes into this combined documentation update.
- Added `Docs/SDS/` draft Software Design Specification documents for future AI, combat, quest, explorer, discovery map, launcher, host editor, loot, networking, plugin, expansion, data-format, gameplay-flag, and ganghouse planning.
- Added this CodeUpdate record and a matching verification report.

## Boundaries

- No gameplay systems are implemented.
- No combat is implemented.
- No character creation is implemented.
- No world simulation loop is implemented.
- No networking expansion is implemented.
- No live editor implementation is added.
- No source-code behavior is changed.
- No `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No raw third-party ZIP, RAR, executable, or reference archive is imported.

## Verification

- GitHub remote truth was fetched before applying this update.
- Local `main` was fast-forwarded to `origin/main` commit `105ed76b`, which contains CodeUpdate-0036.
- The CodeUpdate-0036B and CodeUpdate-0037 ZIP contents were inspected before merging.
- Local verification is recorded in `UpdateManifest.json`.
- GitHub Actions remains pending until the user pushes this update and reports the result.

## Next Recommended Work

After CodeUpdate-0037 is pushed and GitHub Actions is green, continue with one small approved Milestone 2 host/server or planning slice. Do not implement gameplay systems until explicitly approved.

