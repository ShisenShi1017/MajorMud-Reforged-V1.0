# Classic Room Command and Dialogue Audit

Scope: committed Librarium versus generated `Data/Classic`, live gate/object/action projection, and the current ASK binding.
The local `ImportedDatabases/` tree contains only its README; primary WCC/MDB binaries were not available for this pass.
Thus this is exhaustive for the committed decoded records, not a fresh native-binary proof.

## Inventory

- rooms: 26668 committed source records; 26668 generated records; 0 missing from generated.
- exits: 61976 committed source records; 61976 generated records; 0 missing from generated.
- npc_dialogue: 771 committed source records; 771 generated records; 0 missing from generated.
- textblocks: 2279 committed source records; 2279 generated records; 0 missing from generated.
- room_interactions: 284 committed source records; 397 generated records; 0 missing from generated.
- Text-command exit aliases: 675.
- Room-action rooms: 493; source command/room bindings: 1739; executable projected bindings: 437.
- NPC dialogue ownership links: 771; keyword routes: 1241; verified live ASK ability routes: 2.

## Discrepancies

- decoded_effect_not_executable: 1302
- dialogue_keyword_not_live_bound: 1223
- dialogue_zero_target_sentinel: 16
- source_text_command_empty: 1

Every discrepancy is listed with source/room/NPC IDs in `classic_room_command_discrepancies.csv`.

## Ferryman

- `npc.81` owns MDB dialogue root `textblock.77`: borrow, skiff, boat, ferry, silvermere are NPC keywords, not exit commands.
- `exit.map1.2149.s` authoritatively supplies `borrow skiff`, `go skiff`, and `row skiff` to `room.map1.2335`.
- The decoded route continues south to Silvermere dock `room.map1.296`, and north through the pier back to Newhaven dock `room.map1.2149`.
- The local dispatcher regression proves all three crossing aliases and the two-way return path. NPC ASK presentation/effects remain unbound; WCC text payload IDs near root 77 do not match the MDB graph and must not be joined by number.
- A disposable real-character regression crosses by `borrow skiff`, autosaves, reloads, and reconnects in `room.map1.2335`; repository saves are not touched.

## Interpretation

- `decoded_effect_not_executable` is a source-backed command whose full effects have not been shown safe in the live room-script executor; it is intentionally omitted from ROOMCOMMANDS.
- `dialogue_keyword_not_live_bound` is not a missing room action. The live ASK dispatcher currently owns only the two verified ability routes.
- `dialogue_zero_target_sentinel` preserves an explicit decoded no-target branch; it is not a missing textblock.
- The empty text gate `exit.map11.40.w` is a real source gap (`Text: `), not an alias to guess.
- The current ROOMCOMMANDS projection lists gates, materialized room objects, executable room scripts, and live NPC-specific ability ASK routes. The client runs a selected entry through the ordinary server command path.
