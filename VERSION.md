# MajorMUD Reforged Version

Current repository baseline: **CodeUpdate 0133 - Runtime Session Omniverse Evidence**

Status: CodeUpdate0132 room/exit load slice and CodeUpdate0133 runtime-session/evidence update are applied locally with Saves/ and ImportedDatabases/ preserved. Local Codex verification passed.

Scope: 0132 adds generated Classic room identity/exits, runtime room/exit smoke reporting, and --smoke-room-exits. 0133 adds runtime developer-avatar session infrastructure and preserves Omniverse evidence as non-canonical validation material only.

Boundary: oom.1 is configured as an explicit developer diagnostic room only. Town Gates and legacy generic start_room metadata are not treated as verified new-character spawn evidence. The developer avatar is non-persistent and is not a Classic character, Librarium record, generated-data entity, or save record. Combat, magic, NPC AI, spawns, shops, economy, quests, persistence, NPC loading, and broader gameplay are unchanged.

Local verification passed: Python room/exit validator, Server Debug build, generated-data smoke, room-exit smoke, scripted local console movement smoke, Server Release build, Core/server Release build, and Core/server smoke tests.

GitHub Actions remains pending until user push and green report.
