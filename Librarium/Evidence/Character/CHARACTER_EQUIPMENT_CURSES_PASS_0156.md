# Character Equipment Curses Pass 0156

## Promoted behavior

- **PROVEN:** `items.json` carries item abilities in `public_reference_0139.fields.abilities_named`; 15 items carry ability 82 and 3 carry ability 83.
- **STRONGLY CORRELATED:** Ability 82 prevents ordinary removal after the item is equipped.
- **STRONGLY CORRELATED:** Ability 83 includes the same no-remove behavior and retains the item through death while death unequips it.
- **STRONGLY CORRELATED:** Ability 84 removes all currently equipped cursed items.
- **PROVEN runtime invariant:** equipped items cannot bypass unequip validation through drop, give, container, NPC, trade, or generic transfer operations.
- **PROVEN runtime invariant:** curse removal, death transfer, and ordinary equipment transitions are atomic and rollback-safe.

## Provenance

- `Data/Classic/items.json` retained Librarium projection.
- `Data/Classic/abilities.json` records `ability.82`, `ability.83`, and `ability.84`.
- Runtime ownership and single-location invariants in `RuntimeItemsEquipment.cs`.

## Runtime impact

`RuntimeItemDefinition` now owns immutable curse flags. Normal unequip rejects cursed equipment. `RemoveCurse` atomically unequips every cursed item in stable slot/instance order. Direct transfer of equipped items is rejected. Ability 83 items remain character-owned and become unequipped during corpse creation.

## Save impact

No save schema change is required. Curse state is definition-owned and reconstructed from canonical item IDs. Equipment location and ownership remain serialized in `RuntimeItemState`.

## World-state impact

Death transfer now distinguishes ability 83 items from normal possessions. Normal possessions enter the corpse; ability 83 items remain with the character.

## Fallback and unresolved native behavior

Exact messages, any additional remove-curse side effects, dual-wield penalties, layering, duplicate-slot multiplicity, durability, and hidden modifier ordering remain unpromoted. Unsupported removal paths fail closed.
