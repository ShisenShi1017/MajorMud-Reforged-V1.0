# WCC-Labeled WBB Import Report

## Summary

CodeUpdate-0040 preserves the user-provided `wbbmud.zip` archive under `ImportedDatabases/WCCLabeledWBB/` as reference/import material.

The user clarified that this is **WCC labeled WBB** because the same BBS hosted two different games. The archive is therefore tracked with both pieces of context: WCC source labeling and WBB file naming.

## Archive Verification

| Field | Value |
|---|---:|
| Source archive | `wbbmud.zip` |
| Destination archive | `ImportedDatabases/WCCLabeledWBB/SourceArchive/wbbmud.zip` |
| SHA-256 | `360FBDB7CA3145FA48DDC78962E1D34A31502894436926106D454E400FD23B1E` |
| Archive size | `10790327` bytes |
| Extracted files | `240` |
| Extracted uncompressed size | `162612911` bytes |

## Extension Counts

- `.ad`: 7
- `.ans`: 1
- `.ban`: 10
- `.bin`: 3
- `.dat`: 16
- `.dll`: 2
- `.doc`: 2
- `.emb`: 10
- `.exe`: 1
- `.h`: 4
- `.hse`: 133
- `.ini`: 1
- `.lic`: 1
- `.log`: 1
- `.mcv`: 4
- `.mdf`: 2
- `.msg`: 4
- `.not`: 2
- `.nu1`: 1
- `.rel`: 2
- `.rln`: 1
- `.sta`: 14
- `.sto`: 7
- `.sys`: 2
- `.txt`: 2
- `.uic`: 1
- `.zip`: 6

## Policy Result

This update does not convert the archive into live Reforged data. It only preserves the source archive, extracted reference files, and inventories for future import tooling or audits.

## Boundaries Confirmed

- No `Data/` rewrite.
- No gameplay systems.
- No live server dependency on legacy executable or DLL files.
- No changes to existing imported archives outside the new target folder.
