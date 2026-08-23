# DataSimVer0013 — Custom Content Registration

## Objective
Record the user-requested sysop/GM-only Synopsis Room and the originally reported
`code_of_twinks_78` value without modifying legacy `/Data`.

## What was added
- `DataExtensions/custom_rooms_overlay.json`
- No command overlay is valid for `code_of_twinks_78`; the user later confirmed it
  is the configured WCCMMUD SYSOP unlock/key.
- `Simulator/DataSimVer/0013/custom_content_registration.json`

## Placeholder-room hypothesis
Some unusual, unreachable, or non-player-facing rooms may be sysop/GM-only placeholders. This is now documented as a hypothesis only.

## Changes
- `/Data`: unchanged
- Room endpoints promoted: 0
- Special-exit semantics promoted: 0
- Custom overlay rooms: 1
- Custom overlay commands: 0

## Remaining unknowns
- Original engine support for sysop/GM private rooms.
- Exact native WCCMMUD SYSOP/GAMEOP command-to-capability mapping.
- Whether any existing legacy rooms are actual sysop/GM rooms.

## Next pass
DataSimVer0014 should test custom-content overlay loading and continue inventory/economy simulation separately if desired.

## Session health
Good. Baseline remains cumulative and evidence boundaries are preserved.
