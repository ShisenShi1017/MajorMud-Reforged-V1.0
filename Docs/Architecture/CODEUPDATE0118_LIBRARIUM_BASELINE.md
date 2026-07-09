# CodeUpdate 0118 — Librarium Baseline Cleanup and Import Retention

Date: 2026-07-09T18:49:01Z

## Official Architecture

MajorMUD Reforged now uses this rule set as the active baseline:

1. **Librarium is the authoritative source of truth.**
2. **Host/server/editor modifies the Librarium.**
3. **Client receives read-only world-specific Librarium/Data views.**
4. **/Data is generated runtime output per world, not hand-edited source.**
5. **/ImportedDatabases remains retained evidence until the Librarium is confirmed 100% complete.**
6. **Classic definitions are locked/evidence-backed.**
7. **Custom and AI worlds use explicit Librarium overlays/overrides.**

## Runtime Data Flow

```text
Librarium
  Classic definitions
  Custom world overlays
  AI-world rules
  Evidence and validation metadata
        ↓
Validate / Export
        ↓
/Data
  Classic/
  Worlds/<CustomWorld>/
  Worlds/<AIWorld>/
        ↓
Server runtime
        ↓
Client read-only world view
```

## Import Retention Rule

`/ImportedDatabases` is intentionally retained. It is not active runtime data. It is retained source evidence for formula discovery, future re-mining, conflict resolution, and proof traceability.

Do not delete `/ImportedDatabases` until all of these are true:

- The Librarium is fully populated from the imported sources.
- All gameplay-relevant mechanics, fields, flags, formulas, and behaviors are represented in Librarium records.
- Open conflicts are resolved or explicitly documented.
- Runtime and simulator mappings are complete.
- The user explicitly approves import retirement.

## /Data Rule

`/Data` must stay as a generated-output placeholder in Git unless a generated runtime package is deliberately included for a release.

Use:

```bash
python Tools/Librarium/export_librarium_to_data.py --world classic --clean
```

## Cleanup Decision

This update does not remove evidence sources. It reduces confusion by making the roles explicit:

- `/Librarium` = authoritative knowledge and world model.
- `/ImportedDatabases` = retained evidence archive until Librarium completion.
- `/Data` = generated runtime output.
- `/Runtime` and `/Apps/Server` = consumers of generated data.
- `/Apps/Client` = read-only active-world view.
