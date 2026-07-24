# Items Runtime Semantic Exhaustion — CodeUpdate0184

## Executable advance

CodeUpdate0184 adds evidence-gated NPC inventory and equipment reset reconciliation. An authoritative caller supplies an NPC identity, reset key, canonical item, target quantity, and optional legal equipment slot. Existing matching instances preserve identity; only deficits are created; repeated calls at target are idempotent.

## Ownership boundary

Items owns item identity, NPC ownership/location, equipment-slot legality, stack reconciliation, validation, persistence mutation, and rollback. NPC Runtime and World Runtime own spawn/reset scheduling and authoritative target selection.

## Fail-safe behavior

The runtime rejects missing identities, negative targets, illegal equipment slots, equipment quantities above one per slot, and existing quantities above target. It does not silently remove NPC inventory or infer Classic NPC item records.

## Native dependencies

Classic NPC inventory/equipment source records, spawn timers, reset ordering, equipped-drop rules, limited-item suppression, and exact messages remain deferred to the final WCC/WBB/native DLL exhaustion pass.
