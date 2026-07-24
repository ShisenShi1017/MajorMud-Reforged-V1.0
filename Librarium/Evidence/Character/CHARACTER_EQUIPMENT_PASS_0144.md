# Character Equipment Interaction Pass 0144

## Promoted behavior

The generated Librarium projection contains 532 equipment relationships, 17 slot codes, and three promoted equipment rules. Runtime equip and unequip operations preserve single-instance ownership, require the canonical generated slot, reject the `NONE` sentinel, enforce one occupant per owner/slot pair, and apply level, strength, alignment, gender, class, and race restrictions before mutation.

Two-handed equipment excludes `OFF_HAND` and `SHIELD`. CodeUpdate0144 applies this rule both during equip commands and while validating restored persistent state, preventing an invalid save from bypassing command-time checks.

Equipment transitions are atomic. A failed restriction or validation check restores the previous item state. Equipped state persists through `RuntimeItemState` using owner, location kind, location ID, and `EquippedSlot`.

## Preserved unknowns

The native ready/readied distinction, exact dual-wield eligibility and penalties, ring/wrist multiplicity, layering, curse/no-remove behavior, durability effects, hidden modifiers, and final modifier aggregation order remain unresolved. Runtime fails safely rather than assigning guessed behavior.
