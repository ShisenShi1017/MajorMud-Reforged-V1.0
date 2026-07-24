# Character Equipment Modifiers Pass 0145

## Promoted runtime behavior

The generated Librarium item projection contains direct numeric combat fields for all 1950 item records. Runtime now reads and aggregates only these explicit fields for equipped item instances:

- armor class bonus: 565 non-zero records
- damage resistance bonus: 246 non-zero records
- accuracy: 285 non-zero records
- speed: 393 non-zero records

Aggregation is a checked additive sum over authoritative equipped state. Results are recalculated on demand, avoiding stale modifier caches after equip, unequip, restore, death, corpse transfer, or loot.

## Not promoted

Named item abilities, curse/no-remove behavior, layering, dual-wield penalties, durability effects, hidden modifiers, and final cross-category statistic formulas remain unresolved native dependencies. No behavior was invented for them.
