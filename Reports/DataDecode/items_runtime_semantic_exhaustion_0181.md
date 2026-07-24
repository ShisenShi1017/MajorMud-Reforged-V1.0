# Items Runtime Semantic Exhaustion — CodeUpdate0181

## Executable advancement

CodeUpdate0181 adds explicit merchant restock target and identity reconciliation.

- Restock calls require an authoritative merchant identity, canonical item ID, restock key, and target quantity.
- Existing matching stock is counted without replacing or renumbering its runtime instances.
- Only missing stock is created.
- Repeating the same restock target is idempotent and preserves stock identities.
- A target below existing stock fails atomically rather than silently deleting merchant inventory.
- Stackable deficits use projected maximum-stack limits and ordinary destination-stack reconciliation.
- The result reports existing, created, and final quantities plus created runtime identities.

## Evidence boundary

This is proven Reforged compatibility infrastructure. It does not infer Classic merchant schedules, target quantities, finite-stock records, identity reuse, or messages. Those remain native-dependent.
