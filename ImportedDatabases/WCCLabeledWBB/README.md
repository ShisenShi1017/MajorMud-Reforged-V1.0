# WCC-Labeled WBB Reference Archive

This folder preserves the user-provided `wbbmud.zip` archive as imported reference material.

The user identified this as **WCC labeled WBB** because the same BBS hosted two different games. The files themselves use WBB naming, but the source should be tracked as WCC-related reference/import material for future audits.

## Contents

- `SourceArchive/wbbmud.zip` - original uploaded archive preserved unchanged.
- `Extracted/` - safely extracted archive contents for future inspection/import tooling.
- `ArchiveManifest.json` - archive summary, integrity hash, and policy boundaries.
- `FileInventory.csv` - per-file inventory with size and SHA-256 hashes.

## Verification

- Source archive SHA-256: `360FBDB7CA3145FA48DDC78962E1D34A31502894436926106D454E400FD23B1E`
- Source archive size: `10790327` bytes
- Extracted file count: `240`
- Extracted uncompressed size: `162612911` bytes

## Policy

This archive is reference/import material only.

- It must not overwrite canonical `Data/`.
- It must not become a live server runtime dependency.
- Executables, DLLs, logs, and legacy runtime files are preserved only for audit/reference context.
- Any conversion into editable Reforged `Data/` requires a separate reviewed import/reconciliation update.
