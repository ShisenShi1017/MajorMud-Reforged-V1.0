# Items Runtime Semantic Exhaustion — CodeUpdate0164

## Pass scope

Runtime item identity, stack lifecycle, partial transfer atomicity, and portable-container integrity.

## Executable advancement

- Added deterministic stack splitting with a new runtime instance identity.
- Added deterministic stack merging where the target identity survives and the source identity is destroyed.
- Added partial-quantity transfer implemented atomically as split plus transfer.
- Added merge equivalence checks across definition, owner, location, slot, charges, durability, and metadata.
- Added checked quantity arithmetic and optional evidence-projected `maximum_stack` enforcement.
- Added runtime validation for circular container graphs, non-container parents, and owner mismatch inside portable containers.
- Added smoke coverage for split, partial transfer, merge, identity removal, and rollback compatibility.

## Evidence boundary

The generated Classic catalog currently exposes no records with `stackable=true`. The runtime infrastructure is active, but Classic item-specific stack eligibility, maximum stack sizes, and command-visible edge cases remain fail-closed until promoted from MMExplorer, WCC/WBB records, saves, or native execution evidence.

## Remaining Items work

- Decode and promote Classic item flags, categories, uses, limits, restrictions, and per-instance persistence fields.
- Reconstruct creation sources, merchant stock, loot, quest-item, death, reset, and limited-item counting behavior.
- Map item modifiers, abilities, spells, ammunition, keys, consumables, durability, and charges.
- Close native-only stack eligibility and maximum-size semantics during the final native pass if earlier evidence remains insufficient.
