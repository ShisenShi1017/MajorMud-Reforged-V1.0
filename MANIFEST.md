# CodeUpdate0134 Manifest

Patch type: update-only
Baseline: committed CodeUpdate0133 repository
Supersedes: all earlier CodeUpdate0134 archives

## Files

| Path | Bytes | SHA-256 |
|---|---:|---|
| `Apps/Compiler/src/LibrariumValidator.cs` | 3514 | `0ba3deccb7d60ad75c14eea1a4a3f95dfa5a0e1cf80c0f61b2b0cd5e5b36de2c` |
| `Apps/Source/Reforged.Server/RuntimeNpcModels.cs` | 5999 | `d6ff01fc5433247ac428081b02c8feda5314d4913121a3015f024d63652b47dd` |
| `Apps/Source/Reforged.Server/RuntimeNpcSmokeReport.cs` | 1555 | `27566a7dbe0f8db964e159ebb6a5b9f475ebf62713c06827c5694dbc3cc31f46` |
| `Apps/Source/Reforged.Server/RuntimeWorldLoader.cs` | 13949 | `49cc72821e56d6b903542e7b85544fe418e4428fb21ead0f34a069bd7bd5f301` |
| `Apps/Source/Reforged.Server/ServerRuntimeHost.cs` | 13199 | `56414f51ae7cc9d5dddf86b1ee56cb8eeead773fee1d0a9be87c3065af6b940a` |
| `CHANGELOG.md` | 1524 | `a22f71af3f48b2ea8b7533b38bcf8af6d71829e254f13179cbbfd3fd69613cf9` |
| `Data/Classic/spawns.json` | 3419600 | `c8102114ffed20274eeb962d1809250335f4b4b5e5789b15f6021fbe0e03d780` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/REPORT.md` | 1601 | `51e54bd71d27c37bd47f1fff8f3ef36b8c8e46c42843ccb8448bc67304c75d97` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map1-room1457-dark-tunnel.jpg` | 247882 | `eba044d7e5481ead28b6eb3f3825a116424e3dd7abdeaebd766d2f27eea490fa` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map12-room1457-limestone-caverns.jpg` | 331616 | `971d78e954bbbeffcbb8a93d2cd95c8454cfdcf8ff470dd655df43af2209630f` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map15-room1457-old-mine.jpg` | 314897 | `3cef9ff6c869f6750864c9d7e422be11ace191906994a366a39b3218f7ce5a7c` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map16-room1457-deep-jungle.jpg` | 386631 | `22b5115f3753f0ce993638bdeb17e4ea8fcab4284759697dbd5b13f6dda05f1f` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map17-room1457-crystal-lake.jpg` | 444906 | `dd22ffaa2add5733f367c6be6e66b2fcb81f1317a55bb886c33e3784ab5a30a0` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map2-room1457-stone-tunnel.jpg` | 385994 | `8075810d42f2272950668f9ee2f6fcdbc464f3501710fe8d77d4f2358ec4a178` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map6-room1457-redstone-tunnel.jpg` | 489747 | `0e03bd8bb0419fef875b3780ff4e63355da7793fe90569b53ff04fd0c98bd4ed` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map7-room1457-cracked-tunnel.jpg` | 358138 | `b842f5d4c60ae1351adf34bd8c45423ba70ad0fd096714c7500dbd96a59906c2` |
| `Evidence/CodeUpdate0134_LinkedSpawnMapQualification/Screenshots/map8-room1457-underground-lake.jpg` | 383693 | `db9843984c1e33168d71e449881864a3b60ce66a24848c8862f07906389bc19a` |
| `HANDOFF.md` | 1160 | `919ab6c0fd6ac35c48a5d62aa46245997db4ba5f61678031f9ced8924156e95a` |
| `Librarium/Objects/spawns/spawn_records_0122.json` | 3265774 | `fef55342bdb1a53846f70d970a4f1b1854ac3ba20ba22b68b6c100e63ddc758c` |
| `PATCH_INSTRUCTIONS.md` | 1693 | `625d08990127351b3c7e9de1384d8caec390a245fbe036ba0a3ebcd469af051b` |
| `Tools/Validation/validate_codeupdate0134_runtime_npc_loading.py` | 4249 | `664e976d793c22445496c3ea3850e4741fb37848ed0edaf78ffe7365940aab31` |

Total files before manifest: 21

## Scope

- Map-qualified NPC primary room identities.
- Map-qualified linked population identities.
- Monster-group and boss resolution.
- Compiler validation preventing regression.
- Runtime attachment by `(MapId, RoomNumber)`.
- Evidence and validation only; no combat or AI execution.
