# CodeUpdate 0134 - Runtime NPC Loading

## Summary

CodeUpdate 0134 adds static generated NPC definition and room spawn loading to the server runtime.

The runtime now loads:

- `Data/Classic/npcs.json`
- `Data/Classic/spawns.json`

Spawns are resolved through generated `spawn_model.monster_type` values. If a spawn cannot be resolved to a generated NPC definition, the runtime retains the declared generated name and reports the unresolved count instead of guessing.

## Runtime Behavior

- Loads generated NPC definitions keyed by generated `legacy_id`.
- Loads generated spawn records.
- Validates every spawn room against the generated room catalog.
- Indexes static occupants by room.
- Displays occupant names during `look` and movement output.
- Adds `--smoke-npcs` for runtime validation.

## Verification

Local verification passed:

- `python Tools/Validation/validate_codeupdate0134_runtime_npc_loading.py`
- `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore`
- `dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data`
- `dotnet run --project Apps/Source/Reforged.Server -- --smoke-room-exits`
- `dotnet run --project Apps/Source/Reforged.Server -- --smoke-npcs`
- Scripted local console `look`, `north`, `look`, `quit`
- `dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore`
- `dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build`

Verified generated-data metrics:

- NPC definitions: 1,100
- Spawn records: 962
- Resolved spawns: 927
- Unresolved spawns retained without guessing: 35
- Spawn rooms: 883
- Missing room references: 0

The scripted local console smoke starts the non-persistent developer avatar in the explicit diagnostic room, moves north to Estwall Street, and verifies that `look` displays `mercenary`.

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
- Generated data rewrites
- Librarium rewrites
- Imported/reference deletion
- Evidence promotion

`room.1` remains an explicit developer diagnostic room only and is not verified new-character spawn evidence.

## Next Candidate

After the user pushes CodeUpdate 0134 and reports green GitHub Actions, the next narrow server slice may begin combat execution readiness. Combat should still be implemented as reusable engine behavior with Classic rule/data plugs where needed, not as hardcoded Classic-only host logic.
