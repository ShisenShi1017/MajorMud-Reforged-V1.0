# Items Runtime Semantic Exhaustion — CodeUpdate0196

## Executable advancement

- Added deterministic reconciliation for exact duplicate persisted item-instance records.
- Added deterministic reconciliation for exact duplicate corpse records.
- Preserved the surviving runtime identity and every logical field.
- Rejected conflicting duplicates rather than choosing or fabricating state.
- Required the fully reconciled state to pass the complete Items validator.
- Added an explicit repository recovery entry point that rewrites the authoritative save only when exact duplicates were safely removed.

## Evidence boundary

No native corruption-repair, duplicate-selection, identity-remapping, or orphan-repair behavior is inferred. Differing records sharing one identity remain an explicit failure for the final native/file pass.

## Validation

- Exact duplicate item collapse.
- Exact duplicate corpse collapse.
- Conflicting duplicate rejection.
- 1,950 canonical item definitions retained.
