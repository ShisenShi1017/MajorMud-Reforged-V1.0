# CodeUpdate0130 — Combat Formula Resolution & Server Classic Test Readiness

## Summary

CodeUpdate0130 prepares the repository for tomorrow night's Classic server test by resolving the three remaining combat-order conflicts with DLL-derived evidence and adding a generated-data smoke validation path to the server source.

## What changed

- Added uploaded DLL combat resolution evidence:
  - `Evidence/Combat/DLLCombatResolutionOrder_0130.md`
  - `Librarium/Evidence/Combat/DLLCombatResolutionOrder_0130.md`
  - `Librarium/Evidence/Combat/dll_calculate_attack_evidence_0130.json`
- Resolved and closed the three open combat conflicts:
  - Physical Damage Reduction
  - Dodge order
  - Special/backstab defense order
- Updated Librarium formula records and generated `Data/Classic/formulas.json`.
- Updated generated `Data/Classic/conflicts.json` so the conflict catalog no longer reports the three combat conflicts as open.
- Added server generated-world data catalog validation source:
  - `Apps/Source/Reforged.Server/GeneratedWorldDataCatalog.cs`
  - `Apps/Source/Reforged.Server/GeneratedWorldSmokeReport.cs`
- Added server CLI smoke path:
  - `--smoke-generated-data`
  - `--validate-generated-data`
- Added no-SDK validation script:
  - `Tools/Validation/validate_codeupdate0130_server_classic_readiness.py`
- Added validation report:
  - `Reports/CodeUpdates/CodeUpdate0130/server_classic_readiness_validation_0130.json`

## Combat resolution now documented

Per uploaded DLL-derived evidence:

1. Attack-type modifiers
2. Hit chance vs AC
3. Miss check
4. Critical
5. Damage roll
6. Flat DR subtraction
7. Dodge roll
8. Bash/smash multiplier
9. Glance/finalize

Backstab uses AV-DV instead of normal AC math and divides dodge chance by 5. There is no extra special-defense layer.

## Runtime status

Gameplay implementation is not claimed complete in this environment. The server source now has a generated-data validation path, but this environment does not include the .NET SDK.

## Required Codex validation

Run these after extracting the ZIP:

```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```

If those pass, begin Classic test scenarios from generated `Data/Classic`.

## Codex Local Verification

Codex applied the CodeUpdate0130 full-repo overlay locally while preserving `Saves/` and `ImportedDatabases/`. Local verification passed for Server Debug build, Server Release build, `--smoke-generated-data`, `--validate-generated-data`, Python 0130 readiness validation, scripted `--run-local` startup, Core/server Release smoke tests, Compiler Release build, and Compiler.Tests execution. The generated runtime still starts in `Unknown Room` with no exits; that remains a known generated-data/runtime-loader limitation for a later narrow slice.
