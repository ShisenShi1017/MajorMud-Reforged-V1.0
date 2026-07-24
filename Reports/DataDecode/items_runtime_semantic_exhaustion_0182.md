# Items Runtime Semantic Exhaustion — CodeUpdate0182

## Executable advancement

CodeUpdate0182 adds an evidence-gated room-item reset reconciliation primitive. World Runtime remains responsible for scheduling and deciding when a reset fires; Items now owns deterministic instance creation, identity preservation, target reconciliation, stacking, validation, persistence mutation, and rollback.

## Proven/strongly correlated behavior activated

- Room reset identity is explicit: room, canonical item, reset key, and target quantity.
- Existing matching instances retain their runtime identities.
- Only the missing deficit is created.
- Repeated reconciliation at the target is idempotent.
- Existing quantity above target is not silently deleted; the operation fails atomically.
- Stack creation obeys projected maximum-stack limits and normal destination reconciliation.

## Intentionally unresolved

Native reset timers, player-presence suppression, hard/soft reset distinctions, duplicate suppression across nonmatching reset keys, hidden-item reset behavior, and exact Classic messages remain dependent on World Runtime and native evidence.
