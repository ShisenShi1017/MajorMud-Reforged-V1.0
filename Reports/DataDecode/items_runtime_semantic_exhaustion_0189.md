# Items Runtime Semantic Exhaustion — CodeUpdate0189

## Executable advancement

CodeUpdate0189 adds evidence-gated event-item reward and lifecycle cleanup reconciliation. Event orchestration must provide explicit event, character, canonical item, reward or cleanup key, lifecycle reason, and target quantity inputs.

## Runtime behavior

- Event rewards preserve matching runtime identities and create only the target deficit.
- Repeated rewards at the same target are idempotent.
- Event cleanup reduces only matching character-owned event items above an explicit target.
- Cleanup is limited to inventory, equipment, portable-container contents, and bank state.
- Cleanup ordering is deterministic and preserves earlier instance identities.
- `event_cleanup_protected=true` blocks cleanup and rolls back atomically.
- Owner quantity limits and destination stack reconciliation remain active.

## Evidence boundary

This pass does not infer Classic event mappings, timers, quantities, party credit, or messages. Those remain native or cross-subsystem dependencies.

## Remaining event-item work

- Decode event-to-item reward mappings.
- Decode completion, expiration, cancellation, and reset cleanup mappings.
- Recover party/shared event credit.
- Recover world-held and NPC-held event items.
- Recover exact ordering and messages.
