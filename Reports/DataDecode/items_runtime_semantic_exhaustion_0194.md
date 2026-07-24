# Items Runtime Semantic Exhaustion — CodeUpdate0194

## Executable advance

Implemented deterministic, evidence-gated random and rare NPC corpse-loot table evaluation. Explicit entries carry canonical item IDs, unique loot keys, chance basis points, and quantity ranges. Stable SHA-256-derived rolls make repeated evaluation reproducible and auditable while existing corpse-loot reconciliation preserves identity and idempotency.

## Evidence boundary

This is Reforged compatibility infrastructure. It does not claim the native MajorMUD loot-table mappings, RNG, seed selection, probability formula, quantity formula, quality rules, conditional drops, or messages. Without a complete authoritative table and seed, no random loot is created.

## Validation

Deterministic smoke coverage verifies stable outcomes, idempotent repeated reconciliation, chance boundaries, and invalid-chance rollback against the 1,950 canonical item definitions.

## Remaining native dependencies

Native loot source mappings, random/rare table selection, RNG/seeding, quantity rolls, conditional drops, loot quality, limited-item suppression, corpse gold generation, and exact messages remain for the final WCC/WBB/native DLL pass.
