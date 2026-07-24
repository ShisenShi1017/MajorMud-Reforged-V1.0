# Combat & Spell Runtime Update 0150

Update 0150 adds the first shared executable resolution boundary used by both combat and spell paths. It does not promote unknown MajorMUD constants. Instead, all executable constants are supplied by an evidence-bearing profile and invalid or absent profiles fail before mutation.

## Runtime behavior

- Seeded deterministic random generation supports replay.
- Resolution order is explicit: base roll, actor power and flat modifier, critical scaling, resistance reduction, saving throw reduction, final clamp.
- Every intermediate value is returned in a trace with combat instance, world tick, actor, target, definition, confidence and evidence references.
- Integer multiplication uses widened intermediates and truncating integer division.
- Strongly correlated profiles require a safe fallback and evidence references.
- The engine itself performs no character-save or world-state mutation; existing event sinks retain ownership and rollback responsibility.

## Validation

`Validation/validate_combat_spell_update_0150.py` validates JSON, required evidence/fallback fields, deterministic-runtime structure, trace fields and balanced C# structure. `RuntimeCombatSpellResolutionSmoke.Run` verifies replay identity, resistance ordering, bounded output and evidence retention.

## Remaining native dependencies

Exact per-path random ranges, resistance scales, save thresholds, critical thresholds and multipliers remain blocked pending evidence-specific promotion or the final native exhaustion pass.
