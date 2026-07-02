# Validation0011 — Cleanup Flags and Quest Persistence Field Mapping

## Objective
Validation0011 continues from the corrected Validation0010 baseline and targets the two largest remaining field-level blockers:

1. Exact item cleanup flag encoding.
2. Exact quest persistence / player-state field mapping.

The pass does not modify `/Data` unless a direct, field-level mapping is proven.

## Scope
Sources scanned inside the cumulative validation package:

- Current reconstructed `/Data` baseline.
- MBBSEmu validation artifacts already included in the cumulative package.
- MajorMUD disassembly-derived reports already included in the cumulative package.
- MDB extractor tooling and extracted-evidence references already included in the validation track.
- Prior validation reports, manifests, and EngineKnowledge.

## Statistics

- Text/code/report files scanned: 468
- Cleanup/reset/hide/delete signal lines: 19521
- Quest/player/Btrieve/state signal lines: 93294
- Timer/respawn/poll signal lines: 43935
- Search/hidden/random/stat signal lines: 52828
- Cleanup signal-bearing files: 227
- Quest signal-bearing files: 274

## Findings

### Cleanup flag encoding
Validation0011 finds strong recurring evidence that cleanup behavior exists as an engine lifecycle and that the runtime distinguishes between world-state maintenance behaviors. However, the exact data-field encoding for individual item cleanup behavior remains unresolved.

Validated at documentation level:

- Cleanup/reset behavior is an engine-level lifecycle concern.
- Hidden/discoverable state is separate from ordinary visibility.
- Delete/destroy and hide/persist concepts remain plausible behavior categories.

Not promoted:

- Exact destroy-on-cleanup flag.
- Exact hide-on-cleanup flag.
- Exact persistent-item flag.
- Exact mapping from MDB/Data row/column to cleanup behavior.

### Quest persistence fields
The validation package contains strong evidence of persistence architecture and Btrieve/state-management references, but Validation0011 does not yet prove exact quest-field mappings.

Validated at documentation level:

- Player persistence and transient world state are separate architectural concerns.
- Quest/state handling appears tied to persistence records rather than room-only data.
- Quest behavior should not be modeled as simple static room data.

Not promoted:

- Exact quest record fields.
- Exact completed/in-progress flag locations.
- Exact repeatable/one-shot quest marker.
- Exact player-vs-world quest-state split.

## Promotion Review

### Approved

1. **Field-level blocker map** promoted to engine documentation.
   - Cleanup and quest persistence are now tracked as separate field-mapping blockers, not broad unknown subsystems.

### Not Approved

- `/Data` promotions: 0
- Cleanup flag promotions: 0
- Quest field promotions: 0

## Changes

- Added Validation0011 cleanup/quest field-mapping report.
- Updated EngineKnowledge with a Validation0011 section.
- Added changelog, manifest, and session handoff.
- Preserved `/Data` unchanged.

## Remaining Unknowns

- Exact cleanup flag encoding.
- Exact quest persistence record fields.
- Whether cleanup flags live in item definition rows, room-item rows, state records, or combined runtime structures.
- Whether quest state is purely player-based or partly global/world-based.
- Exact linkage between Btrieve record fields and quest state.

## Next Pass

Validation0012 should use a narrower trace strategy:

- Pick one quest-related routine or Btrieve access path.
- Trace field offsets and record access in one path only.
- Produce a candidate offset table with confidence levels.

