# Runtime-0007 Spawn Runtime Strategy

Runtime-0007 adds the spawn runtime foundation.

## What this milestone does

- Indexes canonical room spawn fields from `/Data/rooms.json`.
- Indexes monsters by `legacyNumber` from `/Data/monsters.json`.
- Creates deterministic `SpawnRecord` objects.
- Creates initial scheduled spawn events.
- Preserves unverified mechanics as explicit UNKNOWNs.

## What this milestone does not claim

Runtime-0007 does not prove classic spawn formulas. It does not yet resolve:

- exact monster selection semantics for `monsterType`
- boss spawn rules
- linked spawn rules
- area caps
- despawn behavior
- corpse cleanup
- party/player presence effects
- probability/rand timing adjustments

## Runtime policy

Spawn fields may be loaded and scheduled, but unverified behavior must remain tagged as unknown until supported by MMExplorer, MBBSEmu, source evidence, or runtime logs.
