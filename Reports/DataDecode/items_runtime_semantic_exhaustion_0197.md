# Items Runtime Semantic Exhaustion — CodeUpdate0197

## Executable advancement

- Added explicit validation for contained items whose parent container instance is missing.
- Added explicit validation for corpse-held items whose corpse is missing or already cleaned up.
- Added deterministic, opt-in recovery of missing-container children when an authoritative owner is already persisted.
- Recovered items retain their runtime identity, canonical definition, quantity, charges, durability, and metadata.
- Recovery moves the item to the recorded owner's carried inventory, clears any equipment slot, and records the missing parent identity in metadata.
- Added an authoritative repository entry point that rewrites the save only after the repaired state passes the complete Items validator.
- Kept ownerless orphans, missing corpse parents, owner mismatches, circular containers, and non-container parents fail-closed.

## Evidence boundary

No native orphan repair, lost-and-found, room-drop fallback, or corpse repair behavior is inferred. The active path is explicit Reforged compatibility infrastructure limited to cases where the existing owner field provides one unambiguous safe destination.

## Validation

- Missing portable-container parent reporting.
- Owner-known orphan recovery to carried inventory.
- Recovery provenance persistence.
- Ownerless orphan rejection.
- Missing corpse-parent reporting.
- 1,950 canonical item definitions retained.
