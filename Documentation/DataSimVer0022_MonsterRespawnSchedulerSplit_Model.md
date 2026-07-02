# DataSimVer0022 Runtime Model — Monster Respawn Scheduler Split

This model remains documented-only until confirmed by direct executable, runtime-log, or reverse-engineered evidence.

## User-supplied runtime hypothesis
Normal creatures use an approximately 15-minute respawn cycle. Monsters configured with explicit respawn timers start their timer when they die. Those individual timers continue through the daily reset/cleanup cycle rather than being restarted by reset.

## Simulator interpretation
The simulator should keep two concepts separate:

1. **Room/global spawn evaluation**
   - Uses room spawn fields such as `monsterType`, `respawnMinutes`, `maxAreaCap`, `linkedSpawnRoom`, and `bossTypeId`.
   - The original engine may have checked eligible normal spawns on a global periodic pass.

2. **Death-started configured monster timers**
   - Starts when the monster dies.
   - Continues across daily cleanup/reset.
   - Respawns only when the configured timer expires and occupancy/cap rules allow it.

## Promotion rule
Do not rewrite `/Data` to force 15-minute timing. Existing room-level values must remain authoritative until direct evidence identifies how the engine interpreted them.
