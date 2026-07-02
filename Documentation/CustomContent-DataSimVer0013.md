# Custom Content — DataSimVer0013

This update registers custom server content while preserving the legacy runtime data boundary.

## Synopsis Room
The Synopsis Room is defined as `custom_sysop_synopsis_room` in `DataExtensions/custom_rooms_overlay.json`.

It is intended as a private sysop/GM-only administrative room for synopsis notes, world summary access, and future server-editor integration.

## Command
The command `code_of_twinks_78` is registered in `DataExtensions/custom_commands_overlay.json`.

Intended behavior:
- Normal players: rejected.
- Sysop/GM accounts: routed to the Synopsis Room.
- Hidden from normal player command discovery.

## Data policy
This is custom content, not recovered legacy MajorMUD data. It must not be merged into `/Data` unless the project explicitly enters custom server content mode.
