# MajorMUD Reforged Project Status

This is the current live handoff for Codex, ChatGPT, and human project work.

## Mode

- Active mode: Codex local repository work.
- Repository: `ShisenShi1017/MajorMud-Reforged-V1.0`.
- Local path: `E:\MajorMud-Reforged Github`.
- Current user request: verify, commit, and push completed CodeUpdate0137.

## Current Baseline

- Current Code Update: `0137`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0137 - Complete Classic Gameplay`.
- Milestone 1: complete and frozen.
- CodeUpdate0137 local status: complete.
- GitHub Actions status for this local commit: pending until pushed and confirmed green.

## Architecture Rules

- Librarium is the only gameplay authority.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- `ImportedDatabases/` remains untouched unless the user explicitly approves a reviewed cleanup.
- Preserve `Saves/` and do not commit smoke-save artifacts.

## CodeUpdate0137 Verified Local State

Character Foundation:

- 13 playable races generated from Librarium-backed records.
- 15 playable classes generated from Librarium-backed records.
- 195/195 race/class combinations accounted for.
- Race minimum and maximum stats are generated.
- Class hit point bounds, experience percentages, magery fields, weapon/armour categories, combat level, and ability slots are generated.
- Character creation, save, reload, and start-room smoke pass.
- New character start resolves to `room.2147` / `Newhaven, Adventurer's Guild`.

Items, equipment, and Classic repairs:

- `Data/Classic/items.json` contains 1,950 generated item records.
- `Data/Classic/equipment.json` contains 532 generated equipment records.
- `Data/Classic/equipment_slots.json` and `Data/Classic/equipment_rules.json` are generated.
- Runtime item engine covers create, get, get-all, drop, give, equip, wear, unequip, containers, corpses, loot, duplicate rollback, validation, snapshot/reconnect, and world-state save/load.
- Classic repair validator confirms mask face-slot correction, Lyrist's Companion `spell.1055`, bard 10% shop minimum, cloaked figure 10,000 XP, no `room.unknown`, trigger canonicalization, and runtime correction contracts.

Server/runtime:

- Generated data bootstrap passes.
- Room/exit smoke passes.
- NPC loading smoke passes.
- Spell and spell-execution smoke pass.
- Classic combat and runtime combat smokes pass.
- Actors/abilities, interaction/narrative, services/economy, and world/multiplayer smokes pass.
- Scripted local server session smoke passes.

## Verification

Latest complete local runtime verification:

```text
python Tools\Validation\run_complete_runtime_verification.py
```

Result:

```text
PASS 76/76
Reports/RuntimeVerification/complete_runtime_verification_20260711T143632Z.md
Reports/RuntimeVerification/complete_runtime_verification_20260711T143632Z.json
```

GitHub Actions is not claimed green until the pushed commit is verified.

## Next Step

Commit and push CodeUpdate0137, then wait for green GitHub Actions before beginning the next public update.

## Documentation Cleanup Policy

The repository uses a compact active doc surface. Do not recreate old per-update handoff/changelog/manifest trees unless the user explicitly asks.

Current live continuity should be kept here and mirrored briefly in:

- `AGENTS.md`
- `AI_CONTEXT.md`
- `README.md`
- `VERSION.md`
- `CHANGELOG.md`
- `ReleaseNotes.md`
- `ProjectManifest.json`
- `UpdateManifest.json`
- `Docs/Architecture/PROJECT_ROADMAP.md`

Historical detail should stay in structured evidence/report data when it is still needed, not in duplicate root or Docs handoff chains.
