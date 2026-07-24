# Economy Currency Runtime Pass 0150

This pass replaces single-value-only currency handling as the sole economy primitive with a deterministic denomination-aware wallet layer.

Implemented runtime behavior:

- Positive integral denomination ratios anchored to one base denomination.
- Checked conversion in both directions.
- Canonical change generation.
- Optional data-driven coin weight.
- Data-driven account caps.
- Non-negative balances and guarded debits.
- Deadlock-stable, atomic wallet-to-wallet transfer.
- Full rollback when either side cannot commit.
- Versioned snapshots with total consistency checks.
- Strict JSON persistence validation.

The implementation deliberately does not assign gold, silver, copper, or platinum ratios. Existing native evidence confirms a bank data source but does not yet prove the ratios or bank constants. Those values remain Unknown (Native Dependency), while the runtime mechanism is active and deterministic.

Ownership binding is generic by design. Character, NPC, merchant, bank, corpse, party, and world owners can each hold a wallet without changing transfer semantics.
