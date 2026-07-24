# Items Runtime Semantic Exhaustion — CodeUpdate0183

## Executable advance

CodeUpdate0183 adds evidence-gated portable-container restock reconciliation. An authoritative caller can identify a portable container instance, its owner, a restock key, a canonical item, and a target quantity. The Items runtime preserves matching content identities, creates only the deficit, enforces projected stack maxima and container capacity, and rolls back atomically on invalid or excess state.

## Ownership boundary

Items owns content identity, ownership, container placement, stack reconciliation, capacity validation, and persistence mutation. World Runtime, quests, scripts, events, or other orchestration owns when a restock is requested and where its authoritative target originates.

## Fail-safe behavior

The runtime rejects missing identities, negative targets, non-container targets, owner mismatches, capacity overflow, and existing quantities above target. It does not silently delete contents or infer Classic restock records.

## Native dependencies

Classic container-restock records, timers, lock/open interaction, limited-item suppression, retry behavior, and exact messages remain deferred to the final WCC/WBB/native DLL exhaustion pass.
