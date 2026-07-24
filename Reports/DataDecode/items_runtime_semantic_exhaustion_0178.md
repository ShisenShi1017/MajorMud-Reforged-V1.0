# Items Runtime Semantic Exhaustion — CodeUpdate0178

## Executable advancement

CodeUpdate0178 adds an explicit resurrection restoration path that can recover corpse gold together with corpse-held item roots while preserving the existing item-only behavior.

- `RestoreCorpseItems` remains fail-closed for gold.
- `RestoreCorpseForResurrection(..., restoreGold: true)` returns the full persisted corpse-gold amount and clears it from world state.
- Only the character represented by the corpse may invoke restoration.
- Character/Economy orchestration remains responsible for crediting the returned amount.
- Empty corpses finalize and release their limited-item reservation.
- Item, nested-container, gold, corpse-finalization, and rollback behavior share one atomic boundary.

## Evidence boundary

This is proven Reforged compatibility infrastructure. It does not claim that Classic MajorMUD restored corpse gold during resurrection. Exact Classic policy, penalties, partial restoration, and messages remain native-dependent.
