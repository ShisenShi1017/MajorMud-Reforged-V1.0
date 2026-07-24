# Import, Export, Compatibility, and Migration — Update0029

## Implemented

- Added a host-authorized compatibility coordinator for inspect, validate, import, export, and migration requests.
- Added Classic identifier preservation and explicit rejection of Reforged extension packages in Classic mode.
- Added version monotonicity, payload-size, binary-content, and canonical-text validation.
- Added SHA-256 payload fingerprints and owner-commit boundaries.
- Kept WCC/WBB binary decoding, record semantics, and subsystem commits with their dedicated owners.
- Added `--smoke-import-export-compatibility`.

## Ownership

This subsystem validates compatibility envelopes. It does not decode unknown native records or commit Character, world, item, NPC, quest, or other subsystem state.

## Native dependencies

Exact WCC/WBB versions, binary packing, native import/export commands, duplicate handling, missing-reference behavior, legacy save migrations, and rollback semantics remain for the final native pass.
