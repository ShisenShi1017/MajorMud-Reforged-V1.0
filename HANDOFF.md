# Handoff — Post CodeUpdate0134

CodeUpdate0134 is the NPC loading and map-qualified population milestone.

## Active behavior

- NPC definitions load from generated `npcs.json`.
- `monster_type` is a monster-group ID.
- `npc_legacy_id` is an optional boss/unique NPC ID.
- Primary spawn identity is `(map_id, room_number)`.
- Linked population identity is `(map_id, linked_spawn_room)`.
- The runtime never resolves room numbers globally.
- Linked population rooms do not need independent spawn records.
- Spawns for maps absent from the current generated room slice remain loaded but unattached until those maps are generated.

## Verified totals

- 1,100 NPC definitions
- 962 spawn records
- 748 linked population records
- 0 unqualified spawn identities
- 0 unresolved groups or bosses

## Remaining world-generation boundary

The current generated room slice contains Map 1 only. This is no longer an NPC identity defect. Generating the remaining Classic maps is a separate compiler/world-completeness task.

## Next milestone

CodeUpdate0135 may begin combat execution only after local build, compiler, generated-data, NPC smoke, and core tests pass.
