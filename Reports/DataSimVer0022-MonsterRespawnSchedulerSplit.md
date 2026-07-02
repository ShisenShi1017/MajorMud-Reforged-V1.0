# DataSimVer0022 — Monster Respawn Scheduler Split

## Objective
Document and simulate the user-supplied monster respawn model while preserving evidence-first reconstruction rules.

## What was simulated
- Normal creature respawn using room-level `spawn.respawnMinutes` evidence.
- Explicit monster/death timer concept as a documented hypothesis.
- Reset interaction where individual timers continue through daily cleanup/reset.
- Boss/unique occupancy candidates using `bossTypeId` and `gameLimit` indicators.

## Discoveries
- `/Data/rooms.json` contains room-level spawn records for all 1246 rooms.
- 728 rooms have non-zero `spawn.respawnMinutes` values.
- The most common room respawn minute values are: [(5, 666), (0, 518), (2, 29), (1, 17), (60, 7), (3, 3), (10, 3), (15, 1), (20, 1), (8, 1)].
- 1 room currently has an explicit 15-minute room respawn value in the data. The user-supplied normal-creature ~15 minute model is therefore documented as runtime knowledge pending executable verification, not promoted as a universal data rule.
- 114 rooms have non-zero `bossTypeId`, making them candidates for special/unique monster occupancy rules.
- Monster-level `behavior.respawnHours` exists structurally across 1100 monsters, but 0 records currently contain a non-zero value.
- 465 monsters have non-zero `gameLimit`, which may participate in persistence/cap/unique behavior but is not promoted without proof.

## Statistics
- Monsters scanned: 1100
- Rooms scanned: 1246
- Quest rows scanned: 0
- Rooms with monster type links: 934
- Rooms with non-zero respawn minutes: 728
- Rooms with boss type links: 114
- Monsters with non-zero gameLimit: 465
- Monsters with non-zero respawnHours: 0

## Changes
- Added DataSimVer0022 evidence CSV for room spawn and monster behavior timer fields.
- Added simulator documentation for split scheduling:
  1. normal/global room-spawn evaluation;
  2. individual death-started timers for configured monsters.
- Updated EngineKnowledge with user-provided runtime hypothesis.
- `/Data` was not changed.

## Remaining unknowns
- Whether original runtime normal-monster global cycle is exactly 15 minutes or approximately 15 minutes.
- How room-level `respawnMinutes` interacts with the global scheduler.
- Whether `respawnHours` is unused in this dataset or activated by external/runtime flags.
- Exact boss/unique monster occupancy rules.
- Exact relationship between daily reset, cleanup, and spawn evaluation.

## Next pass
DataSimVer0023 should focus on room occupancy rules: whether a spawn is blocked by existing monsters, players, room state, boss caps, or area caps.

## Session health
Healthy. No sequence conflict. No `/Data` promotion performed.
