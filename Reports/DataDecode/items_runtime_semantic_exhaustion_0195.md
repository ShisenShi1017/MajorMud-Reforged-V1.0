# Items Runtime Semantic Exhaustion — CodeUpdate0195

## Executable advancement

- Added deterministic, evidence-gated NPC corpse-gold reconciliation.
- Explicit gold key, seed, and inclusive range are required.
- Repeated evaluation is idempotent.
- Conflicting pre-existing gold is rejected atomically rather than overwritten.
- Economy remains responsible for crediting looted gold.

## Evidence boundary

No native gold table, RNG, scaling formula, conditional rule, or text output is inferred. Those remain tracked for the final WCC/WBB/native pass.

## Validation

- Deterministic range result.
- Idempotent repeat.
- Conflict rollback.
- Invalid range rollback.
- 1,950 canonical item definitions retained.
