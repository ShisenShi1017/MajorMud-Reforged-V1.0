# Items Runtime Semantic Exhaustion — CodeUpdate0200

## Executable advancement

CodeUpdate0200 adds cross-save ownership coordination. Items can now be loaded or persisted only after both internal validation and externally supplied authoritative identity sets agree.

The coordinator validates Character-owned inventory, equipment, trade and bank state; NPC and merchant ownership; quest and event metadata references; and corpse subject, loot-owner, and limited-item reservation identities.

No ambiguous state is repaired automatically. Missing external identities fail closed so the runtime does not invent deletion, reassignment, or replacement behavior.

## Evidence status

Strongly correlated and active as Reforged compatibility infrastructure. Exact native multi-file transaction ordering remains unresolved.

## Remaining before native-only boundary

- Restart and crash reconciliation across all coordinated subsystem generations.
- Global limited-item reconciliation across persisted scopes.
- Administrative item inspection and controlled mutation paths.
- Final Items exhaustion audit.
