# MajorMUD Reforged Handoff — Post CodeUpdate0134

## Baseline

- Apply to committed CodeUpdate0133 baseline `96834067`.
- CodeUpdate0134 is update-only.

## Implemented

The runtime now loads generated NPC definitions and room spawn records:

`Librarium -> Compiler -> /Data/Classic/npcs.json + spawns.json -> Server runtime`

Runtime behavior:

1. Load NPC definitions keyed by generated `legacy_id`.
2. Load spawn records.
3. Resolve each spawn using `spawn_model.monster_type`.
4. Validate the generated `room_ref`.
5. Index spawns by room.
6. Display present NPC names during `look` and room entry.

## Verified generated-data results

- 1,100 NPC definitions
- 962 spawn records
- 927 spawn records resolve to generated NPC definitions
- 35 unresolved definition references retained without guessing
- 883 rooms contain spawn records
- 0 missing room references
- 0 resolved-name mismatches

## Scope boundary

This update loads static NPC definitions and placements only. It does not execute AI, combat, spells, loot, death, movement, respawn timers, shops, or persistence.

## Next milestone

CodeUpdate0135: combat execution, only after local C# build, NPC smoke, and interactive room occupant checks pass.
