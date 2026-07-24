# CodeUpdate0139 Classic Runtime Audit

Date: 2026-07-17

## Scope

This audit verifies the Classic runtime path after the room-text resolver, generated data, spawn placement, and quest dialogue runtime repairs:

```text
Librarium -> Compiler -> Data/Classic -> Runtime Loader -> Live World -> Telnet Session
```

The production Host still consumes generated `/Data` only. It does not read WCC/WBB files directly at runtime.

## Compiler Output Findings

Command:

```text
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-classic-data
```

Result:

```text
Rooms: 26666
Rooms with descriptions: 26666
Rooms without descriptions: 0
NPC definitions: 1100
Monster definitions: 1089
Spawn groups: 18
Spawn records: 962
Items: 1950
Objects: 288
Doors: 1648
Quests: 2183
Triggers: 367
Shops: 178
Banks: 27
Trainers: 44
```

Required room probes in generated data:

```text
Village Entrance: room.map1.2140 descLength=324
Weapons Shop: room.map1.2141 descLength=329
Arena: room.map1.203 descLength=361
Adventurer's Guild: room.map1.348 descLength=462
Newhaven Arena: NOT FOUND in Data/Classic rooms
```

Conclusion: room descriptions are no longer lost before runtime. The compiler output now contains descriptions for every generated Classic room.

## Runtime Loader Findings

Command:

```text
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-classic-runtime
```

Result:

```text
Rooms loaded: 26666
Rooms with descriptions: 26666
NPC definitions: 1100
Monster definitions: 1078
Spawn groups: 18
Spawn records: 962
Live NPC instances: 1013
Live monster instances: 626
Ground objects: 0
Doors: 1648
Shops: 178
Banks: 27
Trainers: 44
Quests: 2183
Triggers: 367
Unresolved room references: 5
Unresolved NPC references: 0
Unresolved spawn references: 2
Spawn placement validation failures: 2
```

Required runtime probes:

```text
Village Entrance: room.map1.2140 descLength=324 spawnRecords=0 liveNpcs=0
Weapons Shop: room.map2.2566 descLength=391 spawnRecords=1 liveNpcs=1
Arena: room.map1.488 descLength=309 spawnRecords=0 liveNpcs=0
Adventurer's Guild: room.map1.348 descLength=462 spawnRecords=1 liveNpcs=1
Newhaven Arena: NOT FOUND in runtime world
```

Conclusion: runtime loading, description projection, NPC definitions, monster definitions, shops, banks, trainers, doors, quests, and triggers are present. Spawn materialization now fails closed for unresolved map-qualified rooms instead of leaking global/group candidates into arbitrary rooms.

## Focused Placement Finding

`audit room "room.map1.21"` verifies:

- Room description exists in generated data.
- Room description loads into runtime.
- Renderer outputs the description.
- The visible NPC is room-scoped to `room.map1.21`.
- No storm giant, woodelf, boss, or unrelated group candidate leaks into the room.
- Current generated evidence resolves the occupant as neutral `guardsman` (`npc.14`) from `spawn.21.5`; correcting that to `elite guardsman` requires evidence-backed Librarium data correction.

## Remaining Runtime Decode Gaps

- Ground objects are still `0` in runtime audit even though compiler output reports `Objects: 288`; decode and connect static room-object placement/materialization.
- Resolve five generated room references that still point at missing runtime rooms.
- Keep two map-qualified spawn placements classified as invalid/non-spawn: `spawn.288.43` points to non-existent `room.map1.288`, and `spawn.2306.213` points to `room.map1.2306`, which the user verified is a shop with no NPC.
- Determine whether `Newhaven Arena` is a missing alias/probe-name issue or a missing generated room identity.
- Keep exact `remoteaction` native mutation evidence-gated until a native/runtime trace proves behavior.
- Keep Classic completed quests non-repeatable on the same character. Replaying a completed quest requires starting a new character.

## Verification

```text
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-interaction-narrative
python Tools\validator.py
```

Latest local result: `python Tools\validator.py` PASS 13/13.
