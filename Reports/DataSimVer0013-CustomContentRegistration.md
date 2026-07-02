# DataSimVer0013 — Custom Content Registration

## Objective
Add the user-requested sysop/GM-only Synopsis Room and the custom command `code_of_twinks_78` without modifying legacy `/Data`.

## What was added
- `DataExtensions/custom_rooms_overlay.json`
- `DataExtensions/custom_commands_overlay.json`
- `Simulator/DataSimVer/0013/custom_content_registration.json`

## Placeholder-room hypothesis
Some unusual, unreachable, or non-player-facing rooms may be sysop/GM-only placeholders. This is now documented as a hypothesis only.

## Changes
- `/Data`: unchanged
- Room endpoints promoted: 0
- Special-exit semantics promoted: 0
- Custom overlay rooms: 1
- Custom overlay commands: 1

## Remaining unknowns
- Original engine support for sysop/GM private rooms.
- Original command parser handling for custom admin commands.
- Whether any existing legacy rooms are actual sysop/GM rooms.

## Next pass
DataSimVer0014 should test custom-content overlay loading and continue inventory/economy simulation separately if desired.

## Session health
Good. Baseline remains cumulative and evidence boundaries are preserved.
