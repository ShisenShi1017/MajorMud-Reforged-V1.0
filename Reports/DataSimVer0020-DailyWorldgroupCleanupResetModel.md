# DataSimVer0020 — Daily Worldgroup Cleanup / Reset Expected Model

## Objective
Model the user-supplied Worldgroup-era daily reset/cleanup behavior as an expected runtime hypothesis while preserving the evidence-first rule.

## What Was Simulated
No original MajorMUD executable behavior was executed. The pass built simulator artifacts for a daily maintenance cycle in which cleanup can delete destroy-on-cleanup runtime objects and hide other objects in their current room for stat-governed Search rediscovery.

## Discoveries
- `/Data` already separates room object placement into visible `roomItems` and hidden `hiddenItemIds`.
- `skills.json` contains `search` governed by `agi,int`.
- Room spawn records expose `respawnMinutes`; monster records expose timer/population-like fields including `gameLimit`, `regenTime`, and `respawnHours`.
- No direct destroy-on-cleanup or hide-on-cleanup flag semantics were found. The daily cleanup cycle is therefore modeled only as a documented expected behavior.

## Statistics
- Rooms scanned: 1246
- Items scanned: 1950
- Monsters scanned: 1100
- Visible room item links: 103
- Hidden room item links: 268
- Rooms with hidden items: 211
- Rooms with nonzero respawnMinutes: 728
- Monsters with nonzero respawnHours: 0
- Monsters with nonzero regenTime: 310
- Native cleanup/reset/search message clue rows: 70

## Changes
- `/Data` changed: No.
- Added DataSimVer0020 simulator, evidence, validation, documentation, codex, manifest, changelog, and session handoff artifacts.

## Promotion Status
- Room endpoints promoted: No.
- Special-exit semantics promoted: No.
- Cleanup semantics promoted: No; documented only.

## Remaining Unknowns
- Exact Worldgroup daily reset time and scheduler trigger.
- Exact item flag(s) for destroy-on-cleanup versus hide-on-cleanup.
- Exact Search formula and threshold.
- Exact interaction between daily cleanup, monster respawn, population caps, and quest persistence.

## Next Pass
DataSimVer0021 should compare native item records and decoded string catalogs for candidate cleanup-policy fields without changing `/Data`.

## Session Health
Healthy. New window not required yet.
