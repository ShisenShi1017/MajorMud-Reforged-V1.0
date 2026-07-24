# CodeUpdate0153 — Ability Ownership and Lifecycle Exhaustion

This pass reconstructs the complete ability lifecycle proven by the decoded textblock runtime.

- All 188 canonical ability definitions are represented.
- Every `checkability`, `testability`, `failability`, `giveability`, and `addability` edge is linked bidirectionally to its textblock stage.
- Runtime ownership is the character-persisted integer ability map. Missing values read as zero.
- `giveability` replaces the value; `addability` adds a signed delta.
- Dialogue execution captures and restores the complete ability map on rollback.
- Ability values persist across logout/login through character save state.
- No duration, cooldown, death-removal, or automatic stacking rule is encoded by these textblock opcodes.
- No dedicated removal opcode exists in this corpus; explicit replacement with zero is the only proven reset mechanism.

Combat, spell, equipment, race, class, and trainer consumers are not inferred. They remain cross-system evidence targets until their own runtime consumers are proven.
