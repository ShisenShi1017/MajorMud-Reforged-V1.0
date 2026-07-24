# Combat & Spell Runtime Update 0153

Update 0153 adds the deterministic cast-state and timer boundary required between spell-resource reservation and effect execution.

## Executable advance

- `RuntimeSpellCastLifecycle` accepts only canonical `spell.` identifiers with explicit evidence-bearing timing policies.
- Casts transition through `Created`, `Casting`, `ReadyToComplete`, and a terminal state; effect execution cannot occur before the explicit ready gate.
- The scheduler owns a monotonic host world tick and advances ready casts in stable completion-tick then cast-ID order.
- A character or NPC may own only one active cast, cast identifiers are unique, and duplicate or invalid transitions fail closed.
- Interruption is allowed only for reasons explicitly promoted by the selected spell policy. Unpromoted damage, movement, disablement, target loss, disconnect, and native branches remain rejected.
- Resource reservations are started with cast creation and committed or refunded only through terminal lifecycle transitions.
- `spell.1304` remains duration-unresolved and therefore cannot be approximated as instant-cast or assigned invented interruption behavior.

## Ownership

Combat/Spells owns cast IDs, temporary cast state, timer scheduling, transition ordering, interruption validation, and trace generation. Character/NPC systems own actor eligibility, balances, and persistent saves. Effect execution remains behind the existing evidence-backed resolution pipeline.

## Validation

The pass validates mirrored JSON, canonical spell IDs, timer policy shape, state-machine invariants, unresolved-policy fallback, C# structural references, Python syntax, generated index agreement, cumulative preservation, manifest hashes, and ZIP integrity. Smoke vectors cover exact duration boundaries, explicit completion, promoted interruption, unresolved duration, unresolved interruption, monotonic ticks, and deterministic traces.

## Remaining dependency

Exact native cast durations, concentration checks, interruption thresholds, silence/stun/movement behavior, disconnect handling, simultaneous completion ordering, and pending-cast persistence remain unpromoted for later evidence and native passes.
