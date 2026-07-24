# Items Runtime Semantic Exhaustion — CodeUpdate0173

## Executable advance

- Reworked character-death transfer to move only top-level owned item instances into the corpse root.
- Preserved nested portable-container parent relationships and runtime instance identities.
- Recursively cleared descendant ownership while the container subtree is corpse-held.
- Recursively restored descendant ownership when a corpse-root container is looted.
- Added deterministic destination-stack reconciliation for directly looted non-container stacks.
- Reworked corpse decay to recursively remove complete container subtrees instead of removing only direct corpse children.
- Added cycle-safe recursive traversal and atomic rollback.
- Added deterministic nested-container death, loot, and decay smoke coverage.

## Evidence boundary

This pass activates hierarchy preservation as Reforged compatibility infrastructure supported by the established one-owner/one-location model. It does not infer Classic death-protection flags, corpse permissions, decay timers, bound-item exceptions, quest-item exceptions, or limited-item reservation behavior.
