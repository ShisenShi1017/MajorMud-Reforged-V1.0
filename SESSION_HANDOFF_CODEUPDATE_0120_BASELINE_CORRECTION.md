# CODEX HANDOFF - CodeUpdate 0120

Use this repository as the corrected baseline for continued Librarium updates.

## Baseline Rules

- Librarium is source of truth.
- `/Data` is generated output only.
- `/ImportedDatabases` remains until the Librarium is 100% complete.
- Runtime reads generated `/Data`; it does not own gameplay truth.
- Client is read-only.
- Classic is locked/evidence-backed.
- Custom and AI worlds use validated Librarium overlays.

## Corrected Metrics

- Total canonical source files classified: 941
- Text files scanned: 657
- Binary/asset files classified: 284
- Object types: 24
- Relationship edges: 16

## Important Files

- `SOURCE_OF_TRUTH.md`
- `REPOSITORY_RULES.md`
- `Reports/CodeUpdate0120/repository_metrics_0120.json`
- `Reports/CodeUpdate0120/repository_metrics_0120.md`
- `Docs/CodeUpdates/CodeUpdate0120_BaselineCorrection.md`

## Next Task

Implement Step 4: Librarium → `/Data` generator/compiler, while preserving the Librarium-first architecture.
