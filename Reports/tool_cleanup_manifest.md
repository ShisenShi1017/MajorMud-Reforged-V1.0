# Tool Cleanup Manifest

This cleanup preserves local game data, Borin local data, the release app, current source code, current validators, and active Librarium/import/promoter tooling.

Approved removal categories:

- Ignored .NET build outputs under `Apps`, `Core`, and `Tests`.
- Historical one-off tool families no longer referenced by the current validator or runtime:
  - `Tools/FormulaBehavior`
  - `Tools/FormulaGapResolution`
  - `Tools/GuardianHall`
  - `Tools/NpcAi`
  - `Tools/RuntimeConvergence`
  - `Tools/RuntimeTraceHarness`
  - `Tools/Worldgroup`
  - `Utilities`
- Old numbered Librarium one-off scripts:
  - `Tools/Librarium/compile_character_foundation_0137.py`
  - `Tools/Librarium/compile_items_equipment_0137.py`
  - `Tools/Librarium/compile_shops_authority_0010.py`
  - `Tools/Librarium/librarium_gap_audit_0131.py`
  - `Tools/Librarium/phase1_exhaustive_mining.py`
  - `Tools/Librarium/phase2_knowledge_reconstruction.py`
  - `Tools/Librarium/query_host_lifecycle_0079.py`
  - `Tools/Librarium/query_host_timing_0078.py`
  - `Tools/Librarium/query_timing_constants_0082.py`
  - `Tools/Librarium/timing_symbol_trace_0084_0087.py`
  - `Tools/Librarium/verify_librarium_sql_normalization_0129.py`
- Old one-off importer probe:
  - `Tools/Importer/dat_deep_probe_v0007.py`
- Old simulator probe catalogs and unreferenced `Simulator/DataSimVer` artifacts.

Preserved categories:

- `Data` local runtime output for playing locally.
- `ImportedDatabases` reference/source data.
- `Librarium` authoritative data and current compiler/export tooling.
- `AI` and Borin local memory/model assets.
- `Release/MajorMudReforgedApp`.
- `Tools/validator.py`.
- `Tools/Validation`.
- `Sim`.
- Current importer/recovery/promoter tools.
- Current MDB/MMExplorer tools.
- Runtime-referenced `Simulator/DataSimVer` evidence files.

Runtime-referenced simulator files preserved:

- `Simulator/DataSimVer/0008/MonsterAbilityInventory.csv`
- `Simulator/DataSimVer/0008/ShopInventoryItemRefs.csv`
- `Simulator/DataSimVer/0008/ShopServiceInventory.csv`
- `Simulator/DataSimVer/0008/TrainerRoomRefs.csv`
- `Simulator/DataSimVer/0013/custom_content_registration.json`
- `Simulator/DataSimVer/0016/DeathResolutionExpectedModel.csv`
- `Simulator/DataSimVer/0016/MonsterRewardLootMatrix.csv`
- `Simulator/DataSimVer/0017/ItemVisibilityStateMatrix.csv`
- `Simulator/DataSimVer0021/monster_respawn_scheduler_expected_model.json`
- `Simulator/DataSimVer0023/quest_boss_respawn_occupancy_expected_model.json`

Post-cleanup verification:

- `dotnet restore MajorMud.Reforged.sln`: PASS.
- `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore`: PASS, 0 warnings, 0 errors.
- `Tools/validator.py`: PASS, 15/15 current-health checks.
- Validator interpreter used: bundled Codex Python, because the local `python` command resolves to an unavailable Microsoft Store alias in this shell.
- `Runtime` was not consolidated or removed. It remains required by `Sim` validators and `Runtime/test_harness/full_game_test_harness.py`.
- Local `Data`, `AI`, `ImportedDatabases`, `Librarium`, `Saves`, and `Release/MajorMudReforgedApp` were preserved.
