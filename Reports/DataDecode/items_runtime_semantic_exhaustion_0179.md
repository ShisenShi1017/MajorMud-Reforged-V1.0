# Items Runtime Semantic Exhaustion — CodeUpdate0179

## Executable advancement

CodeUpdate0179 adds evidence-gated resurrection equipment restoration without changing the safe default restoration path.

- Corpse creation records the prior slot of top-level equipped items in temporary instance metadata.
- Ordinary `RestoreCorpseItems` restores those items carried and unequipped and removes the temporary slot marker.
- The explicit five-argument resurrection path can restore prior equipment slots when supplied an authoritative character record.
- Slot legality, character requirements, occupied-slot conflicts, handedness, and broken-state checks reuse the normal `Equip` transition.
- A missing character record or any failed equip validation rolls back the entire restoration operation.
- Nested portable-container restoration, owner limits, stack reconciliation, optional gold restoration, corpse finalization, and reservation release remain in the same atomic boundary.

## Evidence boundary

This is proven Reforged compatibility infrastructure. It does not claim that Classic MajorMUD automatically restored equipment during resurrection. Exact Classic selection, conflict displacement, cursed-item handling, and messages remain native-dependent.
