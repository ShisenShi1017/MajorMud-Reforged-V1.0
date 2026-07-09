# CodeUpdate0131 — Librarium Gap Audit + Server Smoke Test Prep

## Purpose
Prepare the current Librarium-generated Classic data for tomorrow's server boot/world-load smoke test.

## Changes
- Added no-runtime Librarium gap audit tool: `Tools/Librarium/librarium_gap_audit_0131.py`.
- Audited generated `Data/Classic` for required runtime families, evidence/runtime/generator mapping coverage, and core relationships.
- Added missing generated formula records used by NPC and spell records:
  - `formula.magic.mr_resist`
  - `formula.magic.mr_damage_reduction`
  - `formula.npc.spell_half_energy_refund`
  - `formula.combat.dodge_raw_caps`
  - `formula.npc.attack_energy_loop`
- Normalized `Data/Classic/rooms.json` spawn references from authoritative `Data/Classic/spawns.json` `room_ref` values.
- Refreshed generated-data manifest metadata to CodeUpdate0131.
- Added Codex handoff for server smoke-test commands.

## Validation
- Runtime gameplay code modified: **No**
- Dotnet build/run performed here: **No** (SDK unavailable in this container)
- No-runtime generated data audit: **PASS**
- Families indexed: **35**
- Generated records indexed: **11,201**
- Missing core relationship refs: **0**
- Warnings: **0**
- Errors: **0**

## Codex test commands
```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```
