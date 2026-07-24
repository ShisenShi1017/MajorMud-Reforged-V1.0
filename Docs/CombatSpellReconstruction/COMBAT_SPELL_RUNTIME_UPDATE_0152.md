# Combat & Spell Runtime Update 0152

Update 0152 adds the spell-resource transaction boundary required before casting can safely mutate character or NPC resources.

## Executable advance

- `RuntimeSpellResourceLifecycle` selects only canonical `spell.` policies and rejects missing or unresolved timing.
- Start processing validates available resources, then performs the policy-selected reserve or commit operation behind a captured resource snapshot.
- Terminal processing commits successful casts or applies evidence-selected refunds for interruption and execution failure.
- Duplicate starts and duplicate terminal transitions fail closed, preventing double charge, double refund, and replay drift.
- Sink failures restore the resource snapshot; failed terminal operations remove their completion latch so the owner can safely retry.
- `spell.1304` is explicitly cataloged as timing-unresolved. Its promoted identity and effect range do not justify inventing resource type, cost, or charge timing.

## Ownership

Combat/Spells owns lifecycle ordering, reservation identity, terminal-state handling, and policy validation. Character/NPC resource sinks own balances, persistent mutation, and save writes. Temporary reservations are not persisted without additional evidence.

## Validation

The pass validates JSON mirrors, canonical spell IDs, unresolved-policy fallback, lifecycle state coverage, C# structural references, Python syntax, generated index agreement, cumulative preservation, manifest hashes, and ZIP integrity. Smoke vectors cover reserve, completion commit, interruption refund, unresolved timing rejection, and duplicate terminal rejection.

## Remaining dependency

Exact native resource types, costs, consumption points, fizzle behavior, target-invalid behavior, resistance/save charging, and concentration interruption windows remain unpromoted for the final evidence/native passes.
