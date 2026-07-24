# Character Inventory Weight Pass 0158

## Promoted behavior

**PROVEN:** all 1,950 Classic item records expose a numeric, non-negative `economy.weight` field in the Librarium-generated `Data/Classic/items.json` projection. Runtime item definitions now own that exact unit weight.

Character carried weight is reconstructed deterministically as `sum(unit weight × stack quantity)` across item instances owned by the character in character inventory, equipment, or nested containers. Instance IDs are ordered canonically for the returned provenance ledger; arithmetic is checked in the 64-bit runtime domain.

Nested contents are counted exactly once because CodeUpdate0157 established recursive owner propagation. Room, corpse, NPC, and trade escrow locations are excluded from carried weight. Weight is definition-derived, so no new save field is introduced and reconnect/load reconstruction is deterministic.

## Fail-closed behavior

Non-numeric, negative, or overflowing item weights are rejected. No carrying limit, encumbrance band, movement/combat penalty, container weight reduction, coin weight, or overflow destination is activated without native evidence.

## Remaining unknowns

Carry-capacity formulas, strength interactions, encumbrance thresholds and penalties, whether containers alter contents' effective weight, coin denomination/weight behavior, and inventory overflow handling remain `UNKNOWN` for later evidence or native reconstruction.
