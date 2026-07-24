# Combat & Spells Runtime Update 0185

This pass completes the final non-native exhaustion matrix across 1930 canonical Combat & Spells definitions. Canonical identity is now separated from executable semantics by a central runtime gate.

## Runtime advancement

- Every canonical spell, evidence-visible weapon, and ability receives one immutable exhaustion classification.
- `RequireExecutable` rejects identity-only, definition-only, and native-blocked records.
- `spell.1` remains an isolated promoted test execution and is not generalized to production spells.
- `item.1622` preserves its proven damage-range binding but remains execution-blocked pending production path/timing semantics.
- `spell.1304` remains native-blocked.

## Result

All non-native evidence-accessible behavior is now either implemented, explicitly classified, or fail-closed. Remaining Combat & Spells unknowns require the final WCC/WBB/native DLL and file pass.
