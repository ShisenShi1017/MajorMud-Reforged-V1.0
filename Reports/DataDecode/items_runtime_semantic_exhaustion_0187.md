# Items Runtime Semantic Exhaustion — CodeUpdate0187

## Advanced executable behavior

- Added evidence-keyed, owner-scoped quest item grant reconciliation.
- Added exact atomic quest item consumption in stable runtime-instance order.
- Preserved existing item identities during idempotent grants.
- Created only target deficits and rejected silent removal above target.
- Rejected insufficient consumption quantities with complete rollback.
- Persisted quest identity, quest owner, grant key, and target quantity metadata.

## Evidence boundary

The runtime does not infer quest-to-item mappings, native opcode behavior, abandon/failure/reset cleanup, party credit, or exact text. Those remain explicit orchestration inputs or native dependencies.

## Remaining quest-item work

- Decode exact grant/check/remove relationships from quest data and native dispatch.
- Recover party credit and shared quest-item behavior.
- Recover abandon, failure, reset, and completion cleanup.
- Recover duplicate prevention rules that span multiple grant keys.
- Recover exact messages and native command ordering.
