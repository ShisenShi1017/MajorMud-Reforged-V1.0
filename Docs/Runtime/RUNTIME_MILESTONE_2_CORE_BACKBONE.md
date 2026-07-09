# Runtime Milestone 2 - Core Runtime Backbone

CodeUpdate 0116 establishes runtime backbone contracts for the server while preserving the evidence-first Classic runtime boundary.

## Scope implemented

- Account identity contract.
- Character identity contract.
- World-isolated character ownership model.
- Three-slot save-slot contract.
- In-memory account and character repositories for runtime verification only.
- Room occupancy and entity registry foundation.
- Runtime event scheduling foundation.
- Spawn scheduling foundation.
- NPC lifecycle foundation.
- Combat loop foundation.
- Magic/cast pipeline foundation.

## Scope intentionally not implemented

- Durable save-file writes.
- Combat formulas, damage, initiative, attack resolution, or death handling.
- Spell formulas, effects, mana costs, targeting, or cast resolution.
- NPC AI behavior.
- Spawn table interpretation.
- Quest, dialogue, script, guild, or economy execution.

## Runtime commands added

- `slots` shows the current account/world save slot contract.
- `entities` shows entity registry status.
- `occupants` shows tracked entities in the current room.
- `npc` shows NPC lifecycle foundation status.
- `combat` shows combat loop foundation status.
- `cast` proves the cast pipeline exists but rejects spell execution until the magic runtime milestone.

## Classic-first rule

Classic remains protected. The foundation does not introduce custom mechanics, AI behavior, imports, exports, or gameplay formula execution. It only creates extension points needed for later evidence-backed implementations.

## Persistence rule

The repositories added in 0116 are in-memory only. They are contracts and runtime verification scaffolding, not durable save storage. Saves and ImportedDatabases are not modified by this update.
