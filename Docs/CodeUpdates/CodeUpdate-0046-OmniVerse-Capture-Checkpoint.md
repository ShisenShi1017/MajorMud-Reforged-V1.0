# CodeUpdate-0046 - OmniVerse Capture Checkpoint

## Purpose

Record and import the current OmniVerse/MudInfo reference capture state before binary-first verification.

This update preserves the gathered API/map evidence under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only. Exploratory HTTrack cache and one-off test files were excluded from the committed payload to keep the final 0046 evidence clean.

## Baseline

- CodeUpdate-0045 is green by user report.
- Local repository was clean before CodeUpdate-0046 started.

## Captures Recorded

### OmniVerse HTTrack Shell

- Path: `E:\Httracks\OmniVerseCodexFull`
- Files: 17
- Result: captured the OmniVerse SPA shell, JavaScript bundle, CSS, and assets.
- Limitation: HTTrack does not enumerate the full codex records by itself because records are loaded from API calls at runtime.

### OmniVerse WCC API

- Source path: `E:\Httracks\OmniVerseCodexApi\wcc`
- Repository path: `ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc`

| File | Records | Bytes | SHA-256 |
|---|---:|---:|---|
| `codex-ability-wcc.json` | 188 | 46,792 | `0DF691178423099A061477D8DB2B5F0653A6861F61ADE98FAAFDF8C5A0FAEA17` |
| `codex-class-wcc.json` | 15 | 4,422 | `2D7638C81A3E7E3ACE0C581E892ED484889F1BEE1667120641B50F2CCFBFFD82` |
| `codex-item-wcc.json` | 1,950 | 1,819,144 | `576E5D2FFF63DA84004F5EF8B42C82545BBC04475E46AFB10C0FE0F5CD130C3A` |
| `codex-monster-wcc.json` | 1,100 | 1,549,498 | `83D8FB4098107324B454DB4299668624A20C838A094204917D75CA4861E12024` |
| `codex-race-wcc.json` | 13 | 4,273 | `60122690E85093D7F4F860955E3BD0AA48CE0FB4E8FC2833FA86EF9AA3349282` |
| `codex-shop-wcc.json` | 178 | 383,545 | `FE9E824BE629D594E29068129DD302DA73A58775400263AAAB4188D6FBEA9AEC` |
| `codex-spell-wcc.json` | 1,350 | 928,400 | `1DD3F35AE5559F1CD5B724D1B74964737A58490B5C4BE564F0E5F0B4993BE0D3` |
| `world-status.json` | 3 | 434 | `E01F452967730DEA6BB2EC60203D864F46EF1F33115A7B7DB53C7BC1B603659E` |

### OmniVerse WCC Rooms

- Capture method: shop-seeded roommap pulls from OmniVerse room/roommap API endpoints.
- Seed count: 168.
- Roommap files: 168.
- Discovered room references: 1,269.
- Room detail files: 1,246.
- Room detail errors: 23, recorded in `room-capture-errors-wcc.csv`.
- Public OmniVerse room IDs still require binary-backed mapping before any canonical promotion.

### MudInfo Maps

- Path: `E:\Httracks\MajorMud - Refoge\www.mudinfo.net\maps\phantoms\maps.rar`
- Repository source archive: `ImportedDatabases/OmniVerseReference/CodeUpdate0046/MudInfoMaps/SourceArchive/maps.rar`
- Repository extracted maps: `ImportedDatabases/OmniVerseReference/CodeUpdate0046/MudInfoMaps/Extracted/`
- SHA-256: `E13D866FFD7DB251AEEFEA41EE3156DCED8B70374ED8BE630DDF90D69AB3B0CE`
- Archive type: RAR5.
- Listed with: `C:\Program Files\7-Zip\7z.exe`
- Contents: 102 files and 2 folders.
- Unpacked size: 969,430 bytes.

## Boundaries

- Captured reference evidence was copied into `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only.
- No canonical `Data/` rewrite.
- No server behavior changes.
- No field promotions.
- No gameplay systems.
- No public-ID to binary-row assumptions.

## Next Step

Run binary-first verification against the imported reference evidence.

Room coverage is partial and shop-seeded. Future data work should expand room coverage where endpoint seeds are still missing, produce discrepancy reports, and keep all OmniVerse/MudInfo data validation-only until binary room IDs and public IDs are explicitly bridged.
