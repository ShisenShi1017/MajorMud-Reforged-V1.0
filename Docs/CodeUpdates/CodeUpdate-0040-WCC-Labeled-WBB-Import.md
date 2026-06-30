# CodeUpdate-0040 - WCC-Labeled WBB Imported Reference Archive

## Purpose

Add the user-provided `wbbmud.zip` archive to `ImportedDatabases/` as reference/import material.

The user clarified that this archive should be treated as **WCC labeled WBB** because the same BBS hosted two different games. The archive file and contained legacy files use WBB names, but the repository should preserve the source context as WCC-related reference material.

## Added

- `ImportedDatabases/WCCLabeledWBB/SourceArchive/wbbmud.zip`
- `ImportedDatabases/WCCLabeledWBB/Extracted/`
- `ImportedDatabases/WCCLabeledWBB/README.md`
- `ImportedDatabases/WCCLabeledWBB/ArchiveManifest.json`
- `ImportedDatabases/WCCLabeledWBB/FileInventory.csv`
- `Reports/CodeUpdates/CodeUpdate-0040/WCCLabeledWBBImportReport.md`

## Boundaries

- No canonical `Data/` rewrite.
- No gameplay implementation.
- No combat, character creation, world simulation, networking expansion, or live editor implementation.
- No use of WBB/WCC executable, DLL, or runtime files as live server dependencies.
- No modification of existing `ImportedDatabases/` content outside the new `WCCLabeledWBB` folder.

## Verification

- Uploaded archive was readable as a ZIP.
- Archive path safety check passed before extraction.
- Extracted file count: `240`.
- Source archive SHA-256: `360FBDB7CA3145FA48DDC78962E1D34A31502894436926106D454E400FD23B1E`.
- Patch package was built as a ChatGPT patch-only ZIP with one top-level folder, `Apply-CodeUpdate-0040.bat`, `Apply-CodeUpdate-0040.ps1`, `UpdateManifest.json`, and `Payload/`.

## Next Step

Apply the patch to the local repository, review the resulting diff, then push and verify GitHub Actions. After green Actions, continue with the next small Milestone 2 host/server or data-readiness scope.
