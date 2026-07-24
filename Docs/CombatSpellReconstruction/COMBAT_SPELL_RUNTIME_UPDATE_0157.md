# Combat & Spells Runtime Update 0157

## Executable runtime advancement

This pass connects active spell-effect ticks to the atomic combat mutation boundary. The effect clock and all touched target states are snapshotted before advancement. Every due periodic tick requires a promoted execution policy and commits in deterministic lifecycle order. Any mutation failure restores both target state and the complete effect lifecycle snapshot.

Dispel selection is now executable for promoted policies. Candidates are filtered by canonical target, category, and optional polarity, then ordered by descending promoted priority, application tick, and canonical effect ID. Removal is bounded by the request maximum and the complete lifecycle is restored if removal fails.

No production spell meaning was inferred. Missing polarity, category, tick disposition, priority, or execution policy fails closed. `spell.1304` remains unresolved.

## Validation

The pass validates canonical identifiers, promoted-policy gates, deterministic tick and dispel ordering, checked magnitude arithmetic, joint rollback, Python syntax, C# structural references, cumulative preservation, manifest hashes, and ZIP integrity.
