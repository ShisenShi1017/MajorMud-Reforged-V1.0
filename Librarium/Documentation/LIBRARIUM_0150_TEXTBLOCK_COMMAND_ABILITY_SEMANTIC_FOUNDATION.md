# CodeUpdate 0150 — Textblock, Command, and Ability Semantic Foundation

This update establishes one machine-readable semantic boundary for the three coupled runtime families.

## Promoted runtime behavior

- The 182 contextual MMExplorer quest-stage scripts are treated as an atomic, source-ordered textblock execution corpus.
- All 2,616 promoted operations use condition preflight followed by ordered effects. Unknown opcodes fail closed and execution rollback restores captured character state.
- The active server command registry is exported as 77 canonical registrations with aliases, minimum unique-prefix lengths, and movement ownership.
- All 188 ability records are validated as canonical `ability.<legacy-id>` objects. Nine abilities are directly checked, granted, or modified by the promoted textblock corpus.
- A runtime catalog now rejects missing contracts, duplicate command registrations, invalid prefix lengths, empty ability lifecycle contracts, and any non-fail-closed textblock policy.

## Evidence boundaries

The current promoted MMExplorer quest-stage corpus contains no opcode that directly invokes a player command token. Its command coupling occurs through shared character state and host-owned world intents. The complete original command handler table, exact native text rendering, and `remoteaction` dispatcher behavior remain native reconstruction dependencies rather than invented behavior.

## Generated artifacts

- `Librarium/BehaviorGraph/textblock_command_ability_semantic_graph_0150.json`
- `Librarium/Reports/textblock_command_ability_semantic_exhaustion_0150.json`
- `Data/Classic/textblock_command_ability_contracts.json`
- `Build/CodeUpdate0150/generate_textblock_command_ability_semantics.py`
