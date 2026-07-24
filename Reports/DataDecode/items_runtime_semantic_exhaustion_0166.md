# Items Runtime Semantic Exhaustion — CodeUpdate0166

## Pass

Durability, break-state, and repair lifecycle phase 1.

## Executable advancement

- Added owner-validated atomic durability damage.
- Rejected zero or negative wear and items without projected durability.
- Floored durability at zero without integer underflow.
- Added explicit persisted `broken=true` state when durability reaches zero.
- Added evidence-gated repair requiring `maximum_durability` metadata.
- Capped repair at the authoritative maximum and cleared stale broken state after positive repair.
- Added validation for durability above maximum and broken-state mismatch.
- Added deterministic smoke coverage for damage, break, repair, cap enforcement, metadata persistence, and cleanup.

## Evidence boundary

The generated Classic item catalog currently contains 1,950 item definitions and no projected positive durability or maximum-durability values. This pass therefore activates the generic item-instance lifecycle infrastructure while refusing to infer which Classic items wear, their maxima, wear formulas, repair costs, break effects, or destruction behavior.

Durability reaching zero preserves the item instance. Combat, Character, and Equipment remain responsible for any final derived-stat or action restrictions once native evidence proves them.

## Remaining durability work

- Decode Classic durability-bearing item families and field widths.
- Recover initial and maximum durability.
- Recover combat, death, environmental, and use wear ordering.
- Recover repair amount, cap degradation, price, eligibility, and merchant behavior.
- Recover exact broken-item effects and whether any item is destroyed or transformed.
- Resolve native save-layout and migration behavior.
