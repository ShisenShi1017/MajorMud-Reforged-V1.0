# Remaining Systems 0009 — World Reset and Cleanup

Implemented a deterministic reset coordinator for room, area, zone, and world scopes. Reset actions are routed to their owning subsystems in explicit order, hard-reset exclusions are preserved, required failures are recorded, and stop-and-rollback policy is supported without duplicating subsystem state mutation.

## Native dependencies

Exact Classic reset categories, ordering, active-player behavior, combat interruption, corpse and limited-item survival, merchant/quest/NPC/event handling, persistence, messages, and hard-reset exception encodings remain assigned to the final native pass.
