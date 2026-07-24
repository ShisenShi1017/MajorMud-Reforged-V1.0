# Character Inventory Pass 0157

## Promoted runtime behavior

Container ownership is now a single recursive ownership tree. Moving a container between a character, room, NPC, or trade location updates every nested item owner in deterministic instance-ID order. Persistent states with a contained-item owner different from the containing item are rejected. This is **PROVEN** from the runtime item-location model and generated container relationships.

Stack splitting and merging are atomic and quantity-conserving. A merge requires the same canonical item, owner, location, equipped state, charges, durability, and metadata. Quantity arithmetic is checked, partial merges preserve the source instance, full merges remove it, and splits create a new unique instance carrying the complete runtime state. This behavior is **STRONGLY CORRELATED** because the generated model explicitly owns stackability, quantity, charges, durability, and metadata, while no Classic record currently enables `stackable`.

## Fail-closed boundaries

Classic automatic stacking, weight, encumbrance, overflow, coin denominations, capacity units, and nested-container weight semantics remain unresolved. No guessed limits or formulas were activated.
