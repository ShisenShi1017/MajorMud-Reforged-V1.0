# Items Runtime Semantic Exhaustion — CodeUpdate0188

## Advanced executable behavior

- Added explicit quest lifecycle cleanup target reconciliation.
- Supports authoritative completion, abandon, failure, and reset orchestration without guessing native triggers.
- Removes only quantity above the supplied target in deterministic order.
- Preserves earlier runtime identities and supports partial stack reduction.
- Added explicit cleanup protection and complete rollback.

## Evidence boundary

The runtime does not infer which lifecycle event cleans which item, the target quantity, party credit, native removal order, or exact messages. Those remain explicit orchestration inputs or native dependencies.

## Remaining quest-item work

- Decode exact completion, abandon, failure, and reset cleanup mappings.
- Recover party and shared quest-item ownership and cleanup.
- Recover cleanup interaction with corpses, trade staging, and world-held quest items.
- Recover native ordering and exact text.
