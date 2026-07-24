# Items Runtime Semantic Exhaustion — CodeUpdate0198

## Implemented

- Added owner/location mismatch validation.
- Added deterministic normalization for owned location IDs.
- Added stale owner cleanup for room and corpse items.
- Added carried-inventory recovery for equipment records missing a slot.
- Added stale non-equipment slot cleanup.
- Added persisted recovery provenance and repository rewrite only after full validation.

## Evidence status

Strongly correlated Reforged compatibility behavior. No claim is made that MajorMUD used the same recovery algorithm.

## Remaining

- Parent-owner mismatch reconciliation remains fail-closed.
- Cross-save ownership and restart coordination remain.
- Native item flags, stack, charge, durability, save layout, exact messages, RNG, and reset timing remain for the final native pass.
