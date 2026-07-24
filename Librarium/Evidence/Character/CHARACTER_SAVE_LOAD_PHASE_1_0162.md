# Character Save / Load Phase 1 — CodeUpdate0162

## Promoted runtime contract

Character JSON persistence now validates and canonicalizes the complete record before writing, performs a serialization round trip, flushes a unique temporary file to disk, and retains the immediately previous validated character image as `.bak`.

Loading is primary-first and fail-closed. A malformed or semantically invalid primary record may recover only from a backup that independently deserializes and passes the Character validator. The recovered record is marked and durably recommitted before it is returned to gameplay.

Every save owns a monotonic persistence sequence and an explicit reason. Disconnect saves project the session's current world and room before commit.

## Classification

- Atomic temporary-file write and flush: **PROVEN** from repository code.
- Deterministic canonicalization, round-trip validation, retained backup, and backup recovery: **STRONGLY CORRELATED** runtime safety behavior.
- Native MajorMUD binary serialization and autosave cadence: **UNKNOWN** and not invented.

## Failure behavior

If neither the primary nor backup is valid, loading stops with an explicit invalid-data failure. No partial/default Character is created.
