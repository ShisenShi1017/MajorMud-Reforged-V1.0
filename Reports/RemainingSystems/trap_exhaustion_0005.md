# Remaining Systems 0005 — Traps

## Executable reconstruction

`RuntimeTrapService` now owns trap definitions, runtime state, detection, visibility, disarming, triggering, charges, rearming, reset timers, optional world-state persistence, and typed downstream effect requests.

Supported locations are room, exit, door, container, item, quest, script, and spell. Supported triggers are enter, exit, open, close, unlock, take, use, search, and script activation.

## Ordering and ownership

A caller resolves the owning room, exit, door, container, item, quest, script, or spell context before invoking the trap. The trap service validates applicability and armed state, applies protection, consumes a charge atomically, updates lifecycle state, and emits typed requests. Combat and Spells resolve damage/status/teleport/summon; NPC AI resolves alarms; Doors and Quests commit their own state changes.

## Evidence-safe limits

Detection and disarm require explicit abilities. Score thresholds are marked evidence-only because exact native rolls, inclusive bounds, failure-trigger behavior, messages, and skill progression remain unresolved. Unknown records and effects are not invented.

## Validation

`--smoke-traps` covers detection, disarm, blocked activation while disarmed, timed rearm, typed effect routing, charge exhaustion, and state invariants.

## Final-native dependencies

Exact DAT record mapping, detection/disarm arithmetic, failed-disarm activation, immunity precedence, messages, charge/reset/rearm rules, persistence, and precise cross-subsystem ordering remain assigned to the final WCC/WBB/native pass.
