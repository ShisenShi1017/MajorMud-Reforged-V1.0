# CodeUpdate0198 — Exact NPC Multi-Step Route Boundary

Implemented a save-safe route execution boundary for patrol, wander, chase, escort, follow, return-home, and scripted movement when an owning host subsystem supplies the complete canonical room sequence.

The runtime validates the living NPC, exact route ID, supported route kind, canonical generated rooms, and origin-room ownership. It persists one active route, commits only an exact next-step traversal completion, advances deterministically, supports explicit repeat routes, and permits exact cancellation. Rejected completions preserve the admitted route for retry.

This pass does not select routes, evaluate exits, derive pathfinding, apply movement costs, choose chase/leash behavior, or infer movement cadence. Those remain native-dependent or owned by other subsystems.
