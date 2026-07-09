# MajorMUD Reforged Repository Rules

CodeUpdate: **0120**

These are the standing repository invariants for the Librarium-first architecture. They remain in force until an explicit future architecture update changes them.

## Source of Truth

1. **The Librarium is the authoritative source of truth.**
2. The host/server/editor writes to the Librarium.
3. The runtime does not own gameplay knowledge.
4. The client is read-only and receives the active world view.

## Generated Runtime Data

5. `/Data` is generated runtime output per world.
6. `/Data` is safe to delete and regenerate.
7. `/Data` must not be hand-edited as authoritative source content.
8. Runtime reads generated `/Data` for speed and stability.

## Evidence Retention

9. `/ImportedDatabases` is retained until the Librarium is 100% complete.
10. Imported evidence is not active gameplay data. It is research/provenance material used to verify and enrich the Librarium.
11. Do not delete canonical imports until the user explicitly declares Librarium completion and archival strategy.

## Worlds

12. Classic is evidence-locked.
13. Custom worlds use explicit Librarium overrides.
14. AI-driven worlds use validated Librarium overlays and host policy controls.

## Development Flow

15. Evidence feeds Librarium records.
16. Librarium records validate/export into `/Data/<world>`.
17. Runtime consumes `/Data/<world>`.
18. Tests and simulators must trace back to Librarium IDs.
