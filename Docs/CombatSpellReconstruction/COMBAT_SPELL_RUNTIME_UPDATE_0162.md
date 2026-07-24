# CodeUpdate0162 — Unified Combat Action Queue

This pass adds an executable shared scheduling boundary for physical attacks, spell completions, and periodic spell effects.

## Runtime behavior

- Actions require canonical action, combat, actor, and owner-runtime references.
- Each action is bound to an evidence-gated same-tick ordering policy.
- Due actions are ordered by due tick, promoted priority, source sequence, then canonical action ID.
- Claiming is deterministic and advances the queue's monotonic world tick.
- Claimed actions may complete or fail exactly once. Queued or claimed actions may be cancelled exactly once.
- Missing native production priority data rejects the action rather than selecting an approximate order.

## Evidence boundary

The shared queue architecture and stable deterministic contract are strongly correlated by the already implemented attack, cast, and effect schedulers. The fixture priority values are test-only and are not claimed as MajorMUD 1.11p constants.

## Remaining native dependency

Exact native precedence among same-tick attacks, spell completions, effect ticks, death, movement, regeneration, summons, and environmental actions remains open for executable/native recovery.
