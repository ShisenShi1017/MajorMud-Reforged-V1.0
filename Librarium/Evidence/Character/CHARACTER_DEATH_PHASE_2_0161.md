# Character Death Phase 2 — CodeUpdate0161

## Promoted behavior

**STRONGLY CORRELATED**: corpse recovery is now owned by the active Character death event. The runtime requires the exact persisted corpse identifier, matching owner, matching death room, a living respawned Character, and a pending recovery state.

Recovery transfers all corpse root items and nested contents, credits escrowed gold with checked arithmetic, marks the corpse cleaned, and writes deterministic recovery provenance. The operation fails closed before save on any mismatch and cannot be repeated.

## Provenance

- CodeUpdate0160 death-event and corpse markers
- Runtime item transaction rollback
- Recursive container ownership contract
- Checked Character gold contract
- Existing respawn/recover command path

## Deliberately unresolved

Native XP loss, partial gold loss, corpse decay timing, resurrection penalties/order, status cleanup, alternate respawn rules, and third-party looting remain **UNKNOWN** for native evidence recovery.
