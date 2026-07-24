# Combat & Spell Runtime Update 0155

Update 0155 closes the executable boundary between a ready cast, evidence-gated target selection, deterministic per-target resolution, atomic effect mutation, resource finalization, and the cast terminal state.

## Executable advance

- `RuntimeSpellCastExecution` accepts only a cast already in `ReadyToComplete` and verifies that cast, actor, and spell identities match the target request.
- Target validation occurs before mutation. A target-policy failure transitions the cast to `Failed` and finalizes the resource transaction as `FailedValidation`.
- Every selected target must have an exact resolution input. Targets execute in the canonical order emitted by `RuntimeSpellTargeting`.
- Resolution IDs are stable per cast and target ordinal. Reforged derives a separate deterministic random stream for each target without claiming that derivation is the original native algorithm.
- Every target snapshot is retained until all target mutations succeed. Any resolution or sink failure restores every captured target and finalizes the cast/resource transaction as `FailedExecution`.
- The cast transitions to `Completed` only after all selected target mutations have committed.

## Ownership

Combat/Spells owns ready-cast execution, target-decision consumption, resolution ordering, mutation atomicity, failure categorization, and terminal cast/resource transitions. Character and NPC systems continue to own authoritative HP, mana, resistance, saving-throw, and other mutable entity state behind the mutation and resource sinks.

## Evidence status

The integration is strongly correlated from the already promoted lifecycle, targeting, atomic sink, and resource terminal contracts. Exact native multi-target rollback behavior, native random-stream splitting, and final resource ordering are retained as explicit dependencies rather than described as proven.

## Validation

This pass validates mirrored Librarium/evidence data, canonical fixture IDs, required ordering and rollback declarations, C# structural references, Python syntax, generated-index agreement, smoke-vector declarations, cumulative preservation, manifest hashes, and ZIP integrity.

## Remaining dependency

`spell.1304` still has no promoted target mode or executable effect profile and remains fail-closed. Status-effect instances, duration/tick processing, dispel/removal, and native multi-target transaction semantics remain for later passes or final native exhaustion.
