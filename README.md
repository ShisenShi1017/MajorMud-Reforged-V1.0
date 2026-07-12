# MajorMUD Reforged

MajorMUD Reforged is a standalone, data-driven game engine and toolset.

## Current Status

Read [Docs/PROJECT_STATUS.md](Docs/PROJECT_STATUS.md) for the live project handoff.

Short version:

- Current Code Update: `0138`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0138 - Server Runtime and Host Enablement`.
- Local verification: PASS 55/55.
- Latest report: `Reports/RuntimeVerification/complete_runtime_verification_20260712T173531Z.md`.
- GitHub Actions is pending until the pushed commit is confirmed green.
- Librarium is authoritative.
- `/Data` is generated runtime output.
- Classic is evidence-locked.
- `ImportedDatabases/` and real `Saves/` must be preserved.

## Verified Runtime Scope

- Character Foundation: generated races/classes, 195/195 race/class combinations, ready creation gate, map-qualified Classic start, and character save/reload smoke.
- Items and equipment: generated item/equipment data plus runtime smoke coverage for inventory, equipment, containers, corpses, loot, validation, rollback, and persistence.
- Server runtime smokes: generated data, room/exits, NPCs, spells, spell execution, combat, combat runtime, actors/abilities, interaction/narrative, services/economy, world/multiplayer, and scripted local console session.
- Host enablement: host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit surfaces.
- Classic repairs: drop-roll uniformity, mask face-slot correction, Lyrist's Companion spell binding, bard shop minimum, cloaked figure XP, and related runtime contracts.
- Librarium data promotion: map-qualified movement records and 0142 textblock quest-flow records are promoted through Librarium, then generated into `/Data`.

## Development Flow

```text
Librarium -> generated /Data/<world> -> Server runtime -> read-only client view
```

Do not hand-edit generated `/Data` as canonical source. Update Librarium/source compilers, regenerate data, then verify.
