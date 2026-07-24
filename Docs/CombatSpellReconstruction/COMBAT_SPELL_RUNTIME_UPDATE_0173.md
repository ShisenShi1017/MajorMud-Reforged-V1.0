# Combat & Spells Runtime Update 0173

CodeUpdate0173 adds an evidence-gated physical attack-path dispatcher over the existing deterministic weapon executor.

## Executable behavior

- Supports one-handed, two-handed, dual-wield, ranged, thrown, natural, shield, and special attack paths.
- Requires promoted strike counts, damage scales, evidence, and safe fallback behavior.
- Validates primary weapon, off-hand weapon, shield, ammunition, and thrown-weapon readiness before execution.
- Produces stable child attack identifiers and deterministic strike seeds.
- Routes each strike through the existing hit, defense, critical, mitigation, HP mutation, rollback, and defeat pipeline.
- Stops later strikes after lethal defeat.
- Rejects duplicate parent sequences and inconsistent path policies.

## Explicit limits

Production record bindings, native off-hand penalties, extra-attack coefficients, exact consumption timing, and native multi-strike rollback remain unresolved and fail closed.
