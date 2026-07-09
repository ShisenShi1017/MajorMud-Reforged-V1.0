# CodeUpdate0130 Report — Combat Resolution and Server Classic Test Readiness

## Result

Status: `PASS_NO_SDK_VALIDATED`

The repository is prepared for Codex/.NET validation of the generated Classic server boot path.

## Resolved combat conflicts

| Conflict | Resolution |
|---|---|
| Physical DR | Flat subtraction after damage roll and before dodge: `damage -= defender_dr_x10 / 10`; net effect is subtract defender DR once per landed hit. |
| Dodge order | Dodge is evaluated after hit chance, critical, damage roll, and DR. Successful dodge negates the whole hit. |
| Special/backstab defense order | Same two-gate order: hit chance first, dodge last. Backstab uses AV-DV and dodge/5; no extra defense layer. |

## Evidence

Primary evidence added from uploaded `message.txt`:

- `Evidence/Combat/DLLCombatResolutionOrder_0130.md`
- `Librarium/Evidence/Combat/DLLCombatResolutionOrder_0130.md`
- `Librarium/Evidence/Combat/dll_calculate_attack_evidence_0130.json`

## Server readiness work

Added a server generated-data catalog validator so the server can validate the generated Classic world before runtime play:

- required generated families exist,
- world/compile manifests exist,
- manifest world IDs match configured world,
- record counts can be read,
- hashes are computed for diagnostics.

Added CLI smoke command names:

- `--smoke-generated-data`
- `--validate-generated-data`

## Local validation in this environment

The no-SDK validator passed:

```bash
python Tools/Validation/validate_codeupdate0130_server_classic_readiness.py
```

The .NET SDK is not installed in this environment, so `dotnet build` and server execution must be run in Codex or your dev environment.

## Next action

Use this ZIP as the upload to Codex, then run:

```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```
