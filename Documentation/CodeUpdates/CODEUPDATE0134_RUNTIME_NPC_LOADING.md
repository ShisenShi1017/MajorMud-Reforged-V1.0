# CodeUpdate 0134 - Complete Map-Qualified NPC Population

## Summary

CodeUpdate 0134 now represents the corrected complete map-qualified NPC population baseline. It supersedes earlier 0134 NPC-loading archives.

The runtime loads:

- `Data/Classic/npcs.json`
- `Data/Classic/spawns.json`

The corrected model treats `spawn_model.monster_type` as a monster group id and `npc_legacy_id` as an optional boss or unique NPC id. Primary spawn identity is `(map_id, room_number)`. Linked population identity is `(map_id, linked_spawn_room)`.

## Runtime Behavior

- Loads generated NPC definitions keyed by generated `legacy_id`.
- Groups NPC definitions by generated behavior `group_id`.
- Loads 962 map-qualified spawn records across 10 maps.
- Resolves monster groups and optional boss NPC definitions.
- Attaches spawns only when the generated room slice contains the matching `(map_id, room_number)`.
- Preserves future-map spawns unattached until those maps are generated.
- Displays static occupant names during `look` and movement output when the attached generated room contains spawns.
- Adds `--smoke-npcs` for runtime validation.

## Compiler Validation

The compiler validator now reports:

- `SPN001` when a spawn record lacks positive `map_id` and `room_number`.
- `SPN002` when a linked population key is unqualified or inconsistent.

## Verification

Local verification passed:

- `python Tools/Validation/validate_codeupdate0132_runtime_room_exit_slice.py`
- `python Tools/Validation/validate_codeupdate0134_runtime_npc_loading.py`
- `dotnet build Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-restore`
- `dotnet run --project Apps/Compiler.Tests --configuration Release`
- `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore`
- `dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-generated-data`
- `dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-room-exits`
- `dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-npcs`
- Scripted local console `look`, `north`, `look`, `quit`
- `dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore`
- `dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build`

Verified generated-data metrics:

- NPC definitions: 1,100
- NPC groups: 30
- Spawn records: 962
- Spawn maps: 10
- Linked population records: 748
- Linked population records missing map context: 0
- Unresolved monster groups: 0
- Unresolved boss definitions: 0
- Primary spawn rooms available in current generated room slice: 275
- Primary spawn rooms outside current generated room slice: 687

The scripted local console smoke starts the non-persistent developer avatar in the explicit diagnostic room and moves north to Estwall Street. After the map-qualified correction, Estwall Street no longer shows the earlier incorrect `mercenary` occupant because non-Map-1 population is no longer attached to the wrong Map 1 room.

## Boundaries

This update does not add:

- NPC AI
- NPC movement
- Combat
- Death handling
- Loot
- Respawn scheduling
- Shops
- Economy
- Quests
- Persistence
- Full multi-map room generation
- Imported/reference deletion
- Evidence promotion

`room.1` remains an explicit developer diagnostic room only and is not verified new-character spawn evidence.

## Next Candidate

After the user pushes the corrected CodeUpdate 0134 baseline and reports green GitHub Actions, the next narrow server slice may begin combat execution readiness. Combat should still be implemented as reusable engine behavior with Classic rule/data plugs where needed, not as hardcoded Classic-only host logic.
