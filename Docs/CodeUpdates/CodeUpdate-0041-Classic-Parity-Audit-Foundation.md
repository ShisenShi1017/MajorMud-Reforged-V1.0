# CodeUpdate-0041 - Classic Parity Audit Foundation

## Purpose

CodeUpdate-0041 adds the first read-only Classic parity audit foundation.

The goal is to begin measuring how close canonical Reforged `Data/` is to reproducing the Classic MajorMUD baseline using every imported/reference source as evidence.

This update does not claim exact parity. It creates the initial inventory/reporting layer that later updates can extend with exact field-level parsers and comparisons.

## Implemented

- Added `ClassicParityAudit` to inventory canonical `Data/` and `ImportedDatabases/`.
- Added `ClassicParityAuditReport`, source summaries, and category summaries.
- Added broad Classic content categories for rooms, monsters, items, spells, shops, quests, classes/races, ganghouses, maps/paths, and raw legacy databases.
- Attached the audit to non-gameplay Server bootstrap.
- Updated Server startup output to print Classic parity inventory status.
- Added smoke coverage for the audit and bootstrap attachment.
- Added documentation describing the audit path toward future Classic reproduction scoring.
- Recorded that all WCC data remains reference-only unless the user explicitly approves a reviewed import/reconciliation update.

## Boundaries

- No `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No Reforged mode, Custom mode, AI systems, alternate gameplay rules, or mode/profile loader implementation.
- No binary WCC/WBB/WSGSERVE parser is added yet.
- No claim of exact MajorMUD parity is made.
- All WCC data remains reference-only unless explicitly approved otherwise.

## Verification

- Local JSON parse passed for `ProjectManifest.json` and `UpdateManifest.json`.
- Server Release build passed with 0 warnings and 0 errors.
- Core.Tests Release build passed with 0 warnings and 0 errors after rerunning sequentially because an initial parallel build hit a transient Core `obj` file lock.
- Smoke tests passed, including Classic parity audit inventory coverage.
- Server bootstrap run passed and printed `ClassicParity: inventory-ready` with gameplay disabled.

## Future Work

Future updates can add exact parsers and comparison rules for each category, then produce a Classic reproduction score such as rooms/items/monsters/spells/shops coverage.
