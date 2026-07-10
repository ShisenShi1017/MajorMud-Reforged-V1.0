# MajorMUD Reforged Version

Current repository baseline: **CodeUpdate 0134 - Complete Map-Qualified NPC Population**

Status: Corrected CodeUpdate0134 map-qualified NPC population is applied locally with Saves/ and ImportedDatabases/ preserved. Local Codex verification passed. The earlier CodeUpdate0134 push was green by user report; this corrected 0134 baseline still requires a new user push and green GitHub Actions report.

Scope: 0134 loads generated Classic NPC definitions from `Data/Classic/npcs.json`, loads generated spawn records from `Data/Classic/spawns.json`, resolves monster groups through generated `spawn_model.monster_type`, resolves optional boss/unique NPCs through `npc_legacy_id`, validates map-qualified primary and linked population identities, and attaches occupants only when `(map_id, room_number)` exists in the current generated room slice.

Boundary: `room.1` remains configured as an explicit developer diagnostic room only. Town Gates and legacy generic start_room metadata are not treated as verified new-character spawn evidence. The developer avatar is non-persistent and is not a Classic character, Librarium record, generated-data entity, or save record. Static NPC placement is visible for inspection only; NPC AI, combat, movement, death, loot, respawn scheduling, shops, economy, quests, persistence, and broader gameplay remain unchanged.

Local verification passed: room/exit validator, corrected NPC loading validator, compiler Release build, compiler tests, Server Release build, generated-data smoke, room-exit smoke, NPC smoke, scripted local console movement smoke, test Release build, and Core/server smoke tests.

GitHub Actions remains pending until user push and green report.
