# Source of Truth Architecture

CodeUpdate: **0120**

MajorMUD Reforged uses a Librarium-first architecture.

```text
Librarium
  ↓ validate/export
/Data/<world>
  ↓
Server Runtime
  ↓
Client read-only active-world view
```

## Librarium

The Librarium is the editable authoritative model for Classic, Custom, and AI-driven worlds. It contains human-readable knowledge, machine-readable definitions, evidence links, conflicts, overlays, validation state, and runtime/export mappings.

## ImportedDatabases

`/ImportedDatabases` is retained as the canonical evidence archive until the Librarium is fully complete. These sources support formula discovery, conflict resolution, and provenance checks. They are not runtime game data.

## Data

`/Data` is generated runtime output. It is intentionally empty in the repository except for a README and `.gitkeep`.

## Runtime

The runtime executes generated world data. It does not edit the Librarium and does not treat `/Data` as authoritative source content.

## Client

The client is read-only. It displays the active world's allowed Librarium/Data view: Classic, Custom, or AI-driven.
