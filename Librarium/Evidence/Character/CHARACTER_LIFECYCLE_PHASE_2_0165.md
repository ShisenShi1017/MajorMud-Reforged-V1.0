# Character Lifecycle Phase 2 — CodeUpdate0165

## Classification

**STRONGLY CORRELATED.** This pass closes the remaining executable lifecycle integrity gap without inventing native timing policy.

## Promoted runtime behavior

Lifecycle events now carry a checked, monotonically increasing sequence and a complete-transition receipt. Login, logout, and disconnect validate phase, session ownership, event type, sequence, transition completion, and current contract provenance before returning or saving a Character. Invalid sequence text, overflow, backward timestamps, stale provenance, incomplete transitions, and detached records retaining a session fail closed.

A stale active session can be recovered only when the supplied stale session exactly owns the Character. Recovery reuses the established disconnect pipeline, including trade cancellation, combat and movement detachment, room/world/session removal, transient cleanup, final validated save, and ownership release. A mismatched session cannot evict the owner.

## Save and world-state impact

The persisted markers `character.lifecycle.sequence` and `character.lifecycle.transition` make lifecycle completion and ordering queryable. Stale-session recovery produces the same persistent disconnect provenance and cleanup markers as an ungraceful disconnect.

## Remaining Unknown native behavior

Exact welcome/event message order; reconnect grace and socket takeover; link-dead combat retention timing; party leadership succession; native trade escrow disposition; offline corpse-decay reconciliation. These remain non-executable evidence for the final native pass.
