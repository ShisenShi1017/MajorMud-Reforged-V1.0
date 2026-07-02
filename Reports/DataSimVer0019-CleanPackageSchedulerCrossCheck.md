# DataSimVer0019 — Clean Package and Scheduler Cross-Check Pass

## Objective
Clean the cumulative ZIP structure and continue evidence-only simulation around world reset scheduling, cleanup lifecycle, hidden vs deleted items, Search mechanics, monster respawn timing, event scheduling, runtime timers, persistent world objects, and quest persistence.

## What Was Simulated
This pass did not execute original MajorMUD runtime behavior. It scanned `/Data` and native message catalogs to build non-promoting evidence matrices and scheduler hypotheses.

## Discoveries
- The package was cleaned to the required top-level structure only.
- Timer-related room and monster fields remain direct table evidence, not runtime proof.
- Native message catalogs contain clue text for timer/search/reset/event-like concepts, but these are string clues only.
- Visible and hidden room objects can be separated from direct fields, but hidden-vs-deleted cleanup behavior is still unverified.
- No direct global world-reset schedule was found or promoted.

## Statistics
- Rooms scanned: 1246
- Items scanned: 1950
- Monsters scanned: 1100
- Scheduler candidate rows: 2132
- Native message clue rows: 128
- Item persistence/cleanup class rows: 1950
- Room visible/hidden object rows: 371
- World reset/timer term clues: 10

## Changes
- `/Data` changed: No
- Removed extra top-level packaging content from the new cumulative ZIP.
- Added DataSimVer0019 simulator, evidence, validation, report, changelog, manifest, documentation, codex handoff, and session handoff files.

## Promotion Status
- Room endpoints promoted: No.
- Special-exit semantics promoted: No.
- Cleanup semantics promoted: No; documented only.

## Remaining Unknowns
- Global world reset cadence and scheduler mechanism.
- Exact cleanup hidden/delete/persistent policy.
- Search stat formula and threshold model.
- Monster respawn timing interpretation.
- Event queue/trigger mechanics.
- Quest persistence storage and reset interaction.

## Next Pass
DataSimVer0020 should compare item/monster gameLimit fields with room placement and spawn tables to model per-entity population caps without promoting runtime behavior.

## Session Health
Healthy. New window not required yet.
