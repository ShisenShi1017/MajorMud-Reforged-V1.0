# MajorMUD Reforged Version

Current repository baseline: **CodeUpdate 0134 - Runtime NPC Loading**

Status: CodeUpdate0134 runtime NPC loading is applied locally with Saves/ and ImportedDatabases/ preserved. Local Codex verification passed.

Scope: 0134 loads generated Classic NPC definitions from `Data/Classic/npcs.json`, loads generated spawn records from `Data/Classic/spawns.json`, resolves spawns by generated `spawn_model.monster_type`, indexes occupants by room, renders static occupant names during `look` and movement output, and adds `--smoke-npcs`.

Boundary: `room.1` remains configured as an explicit developer diagnostic room only. Town Gates and legacy generic start_room metadata are not treated as verified new-character spawn evidence. The developer avatar is non-persistent and is not a Classic character, Librarium record, generated-data entity, or save record. Static NPC placement is visible for inspection only; NPC AI, combat, movement, death, loot, respawn scheduling, shops, economy, quests, persistence, and broader gameplay remain unchanged.

Local verification passed: Python NPC loading validator, Server Release build, generated-data smoke, room-exit smoke, NPC smoke, scripted local console occupant smoke, test Release build, and Core/server smoke tests.

GitHub Actions remains pending until user push and green report.
