# Items Runtime Semantic Exhaustion — CodeUpdate0175

## Executable advancement

- Extended persisted corpse state with optional loot-owner identity and exclusive-loot expiration.
- Added evidence-gated corpse creation overload for authoritative reservation assignment.
- Added a single authorization boundary shared by item loot and corpse-gold loot.
- Added public permission inspection for command, AI, and simulation callers.
- Added atomic denial and expiration behavior.
- Added corpse identity, gold, duplicate-ID, reservation-owner, and timestamp validation.
- Added deterministic smoke coverage for owner access, non-owner denial, rollback, and expiration.

## Evidence boundary

The runtime does not infer a reservation from the dead character, killer, party, room, item flags, or corpse type. Existing unrestricted behavior remains the default. Exact Classic corpse ownership, duration, party sharing, killer priority, NPC behavior, sysop override, and messages remain native dependencies.

## Status

Corpse loot authorization infrastructure is executable and persistent. Classic activation awaits authoritative projection from death/character/NPC/native evidence.
