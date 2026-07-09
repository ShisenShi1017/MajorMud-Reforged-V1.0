# Changelog - CodeUpdate 0116 Runtime Milestone 2 Core Backbone

## Added

- Account identity and character identity persistence contracts.
- World-isolated character identity and save-slot contracts.
- In-memory account and character repositories for runtime verification only.
- Room occupancy and entity registry foundation.
- Spawn scheduling foundation.
- Runtime event scheduling foundation.
- NPC lifecycle foundation.
- Combat loop foundation.
- Magic/cast pipeline foundation.
- Runtime commands for `entities`, `occupants`, `slots`, `npc`, `combat`, and `cast`.

## Changed

- Runtime sessions now carry account and character identity information.
- Runtime state now records account IDs and save slots.
- Local and network sessions are wired through the identity contracts.
- Scheduler bootstrap now registers foundation channels for event, spawn, NPC, and combat loops.

## Not changed

- No durable save-file writes.
- No gameplay formula execution added.
- No combat, magic, NPC AI, spawn, quest, script, dialogue, guild, or economy implementation.
- No Librarium, Saves, or ImportedDatabases changes.
