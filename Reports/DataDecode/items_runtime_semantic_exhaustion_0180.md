# Items Runtime Semantic Exhaustion — CodeUpdate0180

## Executable advancement

CodeUpdate0180 adds an explicit, evidence-gated equipment-conflict displacement path to resurrection restoration.

- Existing restoration remains fail-closed when the prior slot is occupied or handedness rules conflict.
- A new explicit caller decision can displace exact-slot, two-handed, off-hand, and shield conflicts before restoring prior equipment state.
- Conflict selection is deterministic by slot and runtime instance ID.
- Every displacement uses the normal `Unequip` transition, preserving projected `no_remove` and `cursed_no_remove` restrictions.
- The restoration result reports displaced instance identities.
- Any blocked displacement or later equip failure rolls back the entire resurrection operation, including corpse items, equipment state, ownership, gold, and corpse finalization.

## Evidence boundary

This is proven Reforged compatibility infrastructure. It does not activate or claim Classic automatic displacement. Native selection rules, ordering, exceptions, and exact messages remain tracked for the final WCC/WBB/native exhaustion pass.
