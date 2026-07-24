# Remaining Systems Update 0007 — World Containers and Shared Storage

## Implemented

`RuntimeWorldContainerService` now owns world-container definitions, lock state, shared/per-character/organization access, capacity and weight validation, revisioned deposit and withdrawal requests, atomic commit, reset, decay, and optional world-state persistence.

Container operations emit trap trigger requests instead of resolving trap effects. Character inventory remains the owner of inventory-side mutations, while Economy retains currency and valuation arithmetic.

## Validation

`--smoke-world-containers` covers unlocking, trap routing, deposit request and commit, withdrawal request and commit, and state invariants. Revision conflicts and invalid ownership fail closed.

## Native dependencies

Exact Classic record families, capacity/weight formulas, lock/trap ordering, corpse behavior, bank or guild storage ownership, restock/reset semantics, concurrency behavior, and exact messages remain assigned to the final native pass.
