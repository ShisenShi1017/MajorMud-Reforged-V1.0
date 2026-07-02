# Patch 0068 Data Verification and Full Promotion Report

## Scope
Verified the repository `ImportedDatabases/` folder and promoted remaining discovered data into native `/Data` files while preserving imported/original files as reference-only evidence.

## Key promotions
- Replaced minimal room identities with full WCC room-detail records in `Data/rooms.json`: 1246 rooms.
- Added `Data/room_graph.json` from WCC roommap link evidence: 10097 graph edges, 1269 coordinate samples.
- Added `Data/abilities.json`: 188 ability/property definitions.
- Added `Data/trainers.json`: 13 training-room derived trainer locations.
- Added `Data/message_text_catalogs.json` from native MSG/TEXT string catalogs.
- Added `Data/BehavioralRuntime/behavioral_runtime_evidence.json` and `Data/BehavioralRuntime/worldgroup_runtime_evidence_0062_0064.json` for gameplay-running implementation support.

## Guardrails preserved
- `/Data` remains the Reforged runtime source of truth.
- `ImportedDatabases/`, WCC/WBB/MDB, Worldgroup binaries, and previous reverse-engineering outputs remain provenance/reference evidence.
- Unknown semantic fields are preserved rather than discarded, especially room exit parameters, room attributes, spawn fields, and runtime evidence.

## Still unresolved
- Full 26k+ world expansion is not present as full room-detail JSON in the imported folder available to this repo snapshot; 1,246 detailed rooms were available and promoted.
- Spawn semantics still require exact field interpretation.
- Event/opcode semantics and hidden flag bit names remain validation targets.
- No .NET test execution was possible in this container.

## Next recommended patch
0069 should implement validators/tests over the promoted room exits, room graph, shops, monsters, item refs, spells, and behavioral runtime evidence.
