# Session Handoff — CodeUpdate 0118

Current baseline: **CodeUpdate 0118 — Librarium Baseline Cleanup and Import Retention**

## Critical Rules

- Librarium is the authoritative source of truth.
- `/Data` is generated runtime output per world.
- `/ImportedDatabases` stays in repo until Librarium is 100% complete and user approves retirement.
- Host/server/editor modifies Librarium world definitions and overlays.
- Client is read-only and receives the active world's Librarium/Data view.
- Classic definitions are locked/evidence-backed.
- Custom and AI worlds use explicit overlays.

## Next Recommended Work

Continue Librarium enrichment and bind runtime/server/client components to generated world data. Do not delete imported evidence.
