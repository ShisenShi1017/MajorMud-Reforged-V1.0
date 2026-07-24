# CodeUpdate 0159 — Textblock, Command, and Ability Downstream Integration

The closed managed semantic corpus is now exported through a single runtime-validated integration contract for Character, NPC, Combat, Spells, Economy, World, and Session ownership boundaries.

## Implemented

- Every one of the 77 managed commands is assigned to exactly one downstream domain.
- Every one of the 25 decoded textblock opcodes is assigned to exactly one downstream domain.
- Persistent-command and runtime-mutation counts are retained per domain.
- Numeric abilities remain character-persisted integer values.
- Learned skills remain separate skill/cooldown state.
- Textblock clause rollback remains upstream of downstream handler commit and output release.
- Domain handlers retain validation, transaction, rollback, persistence, and world-mutation ownership.
- Native message payloads, native textblock payloads, `remoteaction`, and the original native command table remain opaque and non-executable.

This contract is the authoritative handoff surface for later subsystem merges. It does not invent behavior in those subsystems.
