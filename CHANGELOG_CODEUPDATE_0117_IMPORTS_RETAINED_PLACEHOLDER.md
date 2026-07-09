# CodeUpdate 0117 - Imported Evidence Retained Placeholder

Generated: 2026-07-09T18:39:31Z

## Purpose

Fixes the CodeUpdate 0116 cleanup policy so imported evidence is retained until the Librarium is 100% complete.

## Decisions

- Librarium remains the authoritative source of truth.
- `/Data` remains generated runtime output per world.
- `/ImportedDatabases` is retained as the evidence/source archive for formula and mechanic discovery.
- Evidence reports may reference imported sources; they should not replace the source trees until Librarium completion is explicitly declared.
- Client remains read-only for the active world's Librarium/data view.
- Host/server/editor can modify Librarium branches for custom and AI worlds; Classic remains locked/verified.

## Cleanup policy

Do not delete imported evidence sources yet. They are kept, not defended, as working evidence until the Librarium is complete.
