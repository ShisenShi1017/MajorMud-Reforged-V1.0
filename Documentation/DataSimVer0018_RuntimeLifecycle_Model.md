# DataSimVer0018 — World Reset, Cleanup, Search, Timer Lifecycle Pass

## Objective
Focus on world reset scheduling, cleanup lifecycle, hidden-vs-deleted items, Search mechanics, monster respawn timing, event scheduling, runtime timers, persistent world objects, and quest persistence.

## What Was Simulated
This pass did not execute an original MajorMUD runtime. It built an expected-behavior evidence matrix from the cumulative `/Data` tables only. The simulator classified direct table fields versus hypotheses and refused to promote inferred behavior into `/Data`.

## Discoveries
- Room spawn records expose timer-related fields: `respawnMinutes`, `maxAreaCap`, `linkedSpawnRoom`, and `bossTypeId`.
- Monster behavior records expose lifecycle-related fields: `gameLimit`, `regenTime`, `respawnHours`, and `hpRegen`.
- Room records directly expose hidden item links through `hiddenItemIds`.
- No direct global world-reset schedule field was found.
- No direct cleanup deletion policy field was found.
- `Data/quests.json` currently contains an empty quest array; quest persistence remains clue-based only.

## Statistics
- Rooms scanned: 1246
- Items scanned: 1950
- Monsters scanned: 1100
- Rooms with spawn/timer evidence: 1037
- Monsters with lifecycle/timer behavior evidence: 1086
- Hidden room item links: 268
- Visible room item links: 103
- Cleanup lifecycle candidate rows: 891
- Quest persistence clue rows: 6
- Event/timer clue rows: 4296

## Changes
- `/Data` changed: No.
- Added DataSimVer0018 simulator outputs.
- Added DataSimVer0018 evidence, validation, report, changelog, manifest, documentation, codex handoff, and session handoff files.

## Promotion Status
- Room endpoints promoted: No.
- Special-exit semantics promoted: No.
- Cleanup semantics promoted: No; documented only.

## Remaining Unknowns
- Global world reset cadence and scheduler mechanism.
- Exact cleanup policy for hidden vs deleted vs persistent objects.
- Search stat formula and thresholds.
- Monster respawn timing interpretation for minutes vs hours and gameLimit fields.
- Event scheduling queue semantics.
- Quest persistence storage model.

## Next Pass
DataSimVer0019 should cross-check timer-like fields against native decode/string catalogs and build a non-promoting scheduler hypothesis map by source file and entity type.

## Session Health
Healthy. Window can continue. New window not required yet.
