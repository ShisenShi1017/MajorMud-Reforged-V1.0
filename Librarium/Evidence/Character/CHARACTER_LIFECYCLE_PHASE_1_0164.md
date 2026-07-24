# Character Lifecycle Phase 1 — CodeUpdate0164

## Classification

**STRONGLY CORRELATED** executable runtime behavior. The ordering is derived from the promoted save/load, item ownership, status/timer, statistics, death/corpse, and active-session contracts already present in the cumulative overlay. Native-only timing and message details remain non-executable Unknown evidence.

## Login / reconnect pipeline

1. Canonicalize and validate the Character ID.
2. Reserve exclusive active ownership; duplicate ownership fails closed.
3. Load a validated primary save or validated backup through the existing persistence policy.
4. Validate Character identity and persistent inventory/equipment/container/corpse ownership.
5. Restore ability ownership.
6. Restore flags and status effects.
7. Restore timers.
8. Rebuild derived statistics.
9. Validate death and corpse state.
10. Validate room/world placement.
11. Register world membership, then room membership.
12. Mark active session ownership and lifecycle provenance.

Any failure performs compensating removal of room/world/combat/trade/transient references and releases the reservation. A partially registered Character is never returned.

## Logout / disconnect pipeline

New actions are blocked by transition to `Disconnecting`. Trade, combat, movement/resting, room, world, party/session, and transient references are detached in deterministic order. Persistent timers/status effects remain in the Character record. A validated lifecycle save is performed before active ownership is released. Cleanup is retried fail-closed if any stage throws.

## Provenance and fallback

Markers: `character.lifecycle.phase`, `character.lifecycle.session`, `character.lifecycle.last_event`, `character.lifecycle.provenance`, `character.lifecycle.reconnect`, and `character.lifecycle.cleanup`.

Fallback: reject contradictory state, compensate external registrations, clear active ownership, and leave native-only behavior unexecuted.

## Remaining Unknown native behavior

Exact login/welcome message ordering; reconnect grace and socket takeover policy; link-dead combat retention timing; party leadership succession; native trade escrow disposition; offline corpse-decay reconciliation.
