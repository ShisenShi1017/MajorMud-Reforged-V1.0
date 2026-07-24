# Items Runtime Semantic Exhaustion — CodeUpdate0202

## Executable advancement

CodeUpdate0202 adds global limited-item reservation reconciliation. Explicit owner-scoped limits are aggregated through character inventory, equipment, trade staging, bank storage, portable-container ancestry, and active corpse reservations. Explicit world-scoped limits are aggregated across every persisted instance.

The coordinator rejects malformed limits, inconsistent projected limits, unresolved corpse/container reservation ownership, owner overflows, and world overflows. It does not choose which excess instance to destroy, move, or release because no native ordering evidence has established a safe destructive repair policy.

Validated load/save entry points now combine coordinated save-generation validation, cross-save ownership validation, and global limited-item validation.

## Native boundary status

Items has not yet reached the native-only boundary. Remaining non-native work consists of administrative item operations and the final exhaustion audit. Native flag mappings, scope definitions beyond explicit projections, exact cleanup priority, and Classic messages remain reserved for the final WCC/WBB/native DLL and leftover-file pass.
