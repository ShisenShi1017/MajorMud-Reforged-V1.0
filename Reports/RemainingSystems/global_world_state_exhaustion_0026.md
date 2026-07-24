# Global Flags, Variables, Counters, and World State — Update0026

Implemented an evidence-safe typed world-state coordinator for global, area, zone, event, and module scopes. Definitions declare owner, value type, default, reset policy, persistence, visibility, and authorized mutation sources. Unknown IDs, meanings, bits, masks, and mutation sources fail closed.

## Runtime

- Boolean, checked integer, and bounded text values
- Owner-authorized set and increment operations
- Deterministic reset-by-policy ordering
- Player visibility filtering
- Revision tracking and atomic schema-versioned persistence
- Corrupt snapshot fallback to catalog defaults

## Native dependencies

Exact Classic record families, bit/mask meanings, default values, mutation call sites, reset order, replication, and restart behavior remain assigned to the final native pass.
