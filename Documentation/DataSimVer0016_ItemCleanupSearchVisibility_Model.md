# DataSimVer0016 Item Cleanup / Hidden Search Visibility Hypothesis

Generated: 2026-07-02T17:32:01

## User-provided engine hypothesis

The user reported that some MajorMUD items are affected by cleanup in more than one way:

1. Some items may be removed by cleanup.
2. Some items may be hidden during cleanup rather than deleted.
3. Hidden cleanup items can still be found with the `search` action.
4. Search success is affected by player statistics.

## Evidence status

This is documented as a user-provided runtime-behavior hypothesis. It is **not** promoted into `/Data` and does **not** alter item records, room records, loot tables, or spawn data.

## Expected model impact

Future simulator passes should model item location/visibility as at least three states:

- `visible`: listed or obvious in the room/inventory context.
- `cleanup_hidden_searchable`: not visible after cleanup, but recoverable through `search`.
- `cleanup_deleted_or_removed`: no longer available after cleanup unless respawn/reload rules restore it.

Future search simulation should include stat-gated discovery checks. Candidate stats may include perception/search-related derived values, class/race modifiers, level, hidden skill fields, or engine-specific search formulas once verified through reverse engineering.

## Required future validation

- Identify item flags or room flags that distinguish hidden searchable items from deleted cleanup items.
- Compare original runtime/MBBSEmu cleanup behavior against simulator expectations.
- Determine which player stat or derived stat affects `search`.
- Determine whether repeated search attempts, time, room lighting, class, race, or equipment modifies discovery.

## Promotion policy

No cleanup/search behavior should be promoted into `/Data` until supported by direct evidence from native data fields, original engine reverse engineering, MBBSEmu behavior, or runtime logs.
