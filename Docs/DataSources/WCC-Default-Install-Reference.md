# WCC Default Install Reference

## Status

Reference/import source added during CodeUpdate-0039.

## Source

- User-provided archive: `C:\Users\djkao\Downloads\wcc default.rar`
- Imported location: `ImportedDatabases/WCCDefaultInstall/`
- Preserved source archive: `ImportedDatabases/WCCDefaultInstall/SourceArchive/wcc default.rar`
- Extracted folder: `ImportedDatabases/WCCDefaultInstall/wcc default/`

## Verification

- Archive type: RAR.
- Archive integrity test: passed with 7-Zip.
- Archive top-level folder: `wcc default`.
- Extracted files: 152.
- Extracted size: 159,135,272 bytes.
- Source archive SHA-256: `92FA1CBD86E00154789950FB2BE443E0CB24C735B631BC44DE14531F0D422E5D`.

## File Profile

Notable extracted WCC data/runtime files include:

- `wccupda2.dat`
- `wccmp002.dat`
- `wcctext2.dat`
- `wccitem2.dat`
- `wccmsg2.dat`
- `wccknms2.dat`
- `wccspel2.dat`
- `wccshop2.dat`
- `wccrace2.dat`
- `wccclas2.dat`
- `wccacts2.dat`

The archive also contains legacy executable/runtime artifacts such as `.dll` and `.exe` files. These are reference/import artifacts only.

## Boundaries

- No files under `Data/` were changed.
- This is not a live server runtime dependency.
- This does not approve importing, rewriting, or reconciling data into canonical Reforged `Data/`.
- Any future conversion or reconciliation must be a separate reviewed update.

