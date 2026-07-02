# DataSimVer0017 — Cleanup, Hidden Item Visibility, and Stat-Gated Search Expected Model

## Objective
Model room cleanup and hidden-item discovery using the corrected DataSimVer0016 cumulative package as the baseline.

This pass specifically incorporates the new project hypothesis: some items are not deleted during cleanup but become hidden, and the player must use `search` to find them. Search effectiveness is stat affected.

## Baseline
- Source package: MajorMUD-Reforged-DataSimVer0016-Cumulative.zip
- Sequence status: sequential; 0016 exists and 0017 is the next valid pass.

## Evidence used
- `rooms.json` contains visible `roomItems` and separate `hiddenItemIds` fields.
- `skills.json` contains a `search` skill described as finding hidden items and clues.
- The `search` skill is governed by `agi` and `int`.
- `skills.json` also contains `perception`, which may later participate in hidden danger/exit/clue checks, but no runtime formula is promoted.

## What was simulated
- Visible room item states.
- Hidden searchable room item states.
- Cleanup candidate classes from item metadata.
- Five virtual searcher profiles using `agi`, `int`, and `search_skill`.
- Search attempts against all hidden room item references.

## Discoveries
- The data model already supports a visible-vs-hidden item distinction.
- Hidden item discovery has direct data support through `hiddenItemIds` plus the `search` skill.
- Stat influence has direct skill-definition support: `search` is governed by `agi` and `int`.
- Cleanup timing, reset cadence, and exact hide-vs-delete engine behavior remain runtime hypotheses and were not promoted into `/Data`.

## Statistics
- Rooms analyzed: 1246
- Items analyzed: 1950
- Rooms with visible room items: 82
- Visible room item refs: 103
- Rooms with hidden item IDs: 211
- Hidden item refs: 268
- Total room item visibility refs: 371
- Resolved room item refs: 267
- Unresolved room item refs: 104
- Search attempts logged: 1340
- Destroy-on-death cleanup candidates: 417
- Static/not-gettable cleanup candidates: 77
- Game-limited cleanup-sensitive candidates: 179
- Hidden/find/search message text clues: 56

## Changes
- `/Data`: unchanged.
- Room endpoints promoted: 0.
- Special-exit semantics promoted: 0.
- Cleanup semantics promoted: 0.
- Runtime behavior promoted into `/Data`: false.

## Remaining unknowns
- Exact cleanup timer/reset cadence.
- Whether cleanup hides visible items, deletes specific items, or applies both based on item flags.
- Exact search success formula.
- Whether `perception` modifies passive discovery separately from active `search`.
- Whether class/race/level modifies search.
- Whether room light level, effects, stealth, or equipment modify hidden-item discovery.

## Next pass
DataSimVer0018 should model reset scheduling and world refresh policy: room cleanup timers, monster respawn timers, item hide/delete rules, hard-reset monsters, and full-world wipe/reset semantics.

## Session health
Window health is good. Numbering is now sequential again after the corrected 0016 recreation.
