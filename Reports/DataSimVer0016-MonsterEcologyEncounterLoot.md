# DataSimVer0016 — Monster Ecology, Spawn Pressure, Loot/Death Resolution, and Encounter Routing

## Objective
Measure monster ecology and encounter/reward pressure from the actual cumulative package. This pass does not execute the original engine; it builds an expected behavior model from `/Data`.

## Baseline
- Source package: MajorMUD-Reforged-DataSimVer0015-Cumulative.zip
- Sequencing note: DataSimVer0016 is not present in this window/package. This 0017 pass was executed from the latest available real baseline and records the gap rather than inventing 0016 outputs.

## What was simulated / measured
- Room spawn records with nonzero monster type.
- Monster-to-room placement pressure.
- Adjacent encounter routing from spawn rooms.
- Monster reward records: XP, currency, attack count, spell presence, loot table entries.
- Expected death resolution rows for each resolved spawn room.

## Discoveries
- Spawn ecology is strongly table-driven: rooms point to monster type identifiers and respawn settings.
- Death/reward handling can be modeled as a generic reward lookup, but corpse handling, rollback, and exact loot rolls remain unverified runtime rules.
- Encounter routing can be approximated from room exits around spawn rooms, but pathing/chase behavior remains an engine rule requiring original runtime or MBBSEmu evidence.

## Statistics
- Rooms analyzed: 1246
- Monsters analyzed: 1100
- Items analyzed: 1950
- Loot tables analyzed: 420
- Spawn rooms with monsterType: 934
- Resolved spawn monster refs: 927
- Unresolved spawn monster refs: 7
- Loot entry refs: 732
- Resolved loot entry refs: 0
- Unresolved loot entry refs: 732
- Death resolution scenarios: 934
- Encounter routing rows: 934

## Changes
- `/Data`: unchanged.
- Room endpoints promoted: 0.
- Special-exit semantics promoted: 0.
- Runtime behavior promoted into `/Data`: false.

## Remaining unknowns
- Exact respawn timer execution.
- Linked spawn room semantics.
- Area caps and game limits.
- Corpse persistence.
- Exact loot roll algorithm and drop percentages.
- Chase/follow pathing and leash rules.
- Death penalty and player recovery mechanics.

## Next pass
DataSimVer0018 should reconcile quest/flag/event progression with combat/economy state, or DataSimVer0016 can be recreated as the missing intermediate pass if strict numbering continuity is desired.

## Session health
Window health remains good. Package health is good, but numbering continuity contains a documented 0016 gap.
