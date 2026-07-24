# Items Runtime Semantic Exhaustion — CodeUpdate0165

## Pass

Charge, consumption, and destruction lifecycle phase 1.

## Executable advancement

- Added owner-validated atomic charge expenditure.
- Added bounded recharge with an evidence-gated `maximum_charges` projection.
- Added explicit consumable quantity mutation and destruction results.
- Preserved zero-charge non-consumable instances while rejecting further use.
- Prevented consumption of non-empty portable containers.
- Added invalid-state detection for negative charges, negative durability, and projected charge overflow.
- Added deterministic smoke checks for charge decrement, recharge, overflow rollback, quantity decrement, and final-instance destruction.

## Evidence boundary

The generated Classic item catalog exposes `consumable` and `default_charges`. Those fields can safely initialize and validate instance state. It does not presently expose authoritative maximum-charge, recharge-timer, failed-use consumption, residue-item, or empty-container replacement semantics. Those behaviors remain fail-closed and tracked for native exhaustion.

## Runtime ownership

- Items owns current charge count, quantity, destruction, and persistence mutation.
- Commands owns parsing and dispatch.
- Spells and Abilities own invoked effects.
- Character owns final resource and status mutation.
- World Runtime owns recharge scheduling when proven.

## Remaining Items work

- Decode Classic item category/type/flag semantics.
- Promote actual Classic stackable definitions and stack maxima.
- Recover item-specific charge maxima, costs, recharge behavior, and zero-charge outcomes.
- Decode durability fields and wear/repair behavior.
- Complete restrictions, modifiers, equipment relationships, merchant stock, NPC loot, quest items, limited counts, reset/restock, and save-layout semantics.
- Resolve native-only edge cases in the final WCC/WBB/native DLL pass.
