# MajorMUD Reforged

MajorMUD Reforged is a standalone, data-driven game engine and toolset.

## Current Status

Read [Docs/PROJECT_STATUS.md](Docs/PROJECT_STATUS.md) for the live project handoff.

Short version:

- Current Code Update: `0137`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0137 - Complete Classic Gameplay`.
- Local verification: PASS 76/76.
- Latest report: `Reports/RuntimeVerification/complete_runtime_verification_20260711T143632Z.md`.
- GitHub Actions is pending until the pushed commit is confirmed green.
- Librarium is authoritative.
- `/Data` is generated runtime output.
- Classic is evidence-locked.
- `ImportedDatabases/` and `Saves/` must be preserved.

## Verified Runtime Scope

- Character Foundation: generated races/classes, 195/195 race/class combinations, ready creation gate, Newhaven start at `room.2147`, and character save/reload smoke.
- Items and equipment: generated item/equipment data plus runtime smoke coverage for inventory, equipment, containers, corpses, loot, validation, rollback, and persistence.
- Server runtime smokes: generated data, room/exits, NPCs, spells, spell execution, combat, combat runtime, actors/abilities, interaction/narrative, services/economy, world/multiplayer, and scripted local console session.
- Classic repairs: drop-roll uniformity, mask face-slot correction, Lyrist's Companion spell binding, bard shop minimum, cloaked figure XP, and related runtime contracts.

## Development Flow

```text
Librarium -> generated /Data/<world> -> Server runtime -> read-only client view
```

Do not hand-edit generated `/Data` as canonical source. Update Librarium/source compilers, regenerate data, then verify.
