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
- Room-action rooms: 493; source command/room bindings: 1739; executable projected bindings: 1739.
- NPC dialogue ownership links: 771; keyword routes: 1241; live decoded ASK routes: 1225; direct GREET ownership routes: 37 across 19 source roots.

## Discrepancies

- None.

## Intentional Non-Actions

- Dialogue target `textblock.0` sentinels: 16; these remain silent and non-mutating.
- Empty native text-command exits: 1; no alias is fabricated.

Every discrepancy is listed with source/room/NPC IDs in `classic_room_command_discrepancies.csv`.

## Ferryman

- `npc.81` owns MDB dialogue root `textblock.77`: borrow, skiff, boat, ferry, silvermere are NPC keywords, not exit commands.
- `exit.map1.2149.s` authoritatively supplies `borrow skiff`, `go skiff`, and `row skiff` to `room.map1.2335`.
- The decoded route continues south to Silvermere dock `room.map1.296`, and north through the pier back to Newhaven dock `room.map1.2149`.
- The local dispatcher regression proves all three crossing aliases and the two-way return path. Ferryman NPC keywords resolve through the same decoded dialogue executor used world-wide.
- A disposable real-character regression crosses by `borrow skiff`, autosaves, reloads, and reconnects in `room.map1.2335`; repository saves are not touched.

## Interpretation

- Every decoded room action whose operations belong to the exhausted 42-opcode vocabulary is projected through the production executor.
- Every nonzero NPC keyword target resolves through NPC-specific ASK; ability-acquisition routes retain their specialized authority.
- `dialogue_zero_target_sentinel` is an explicit decoded no-target branch and executes as a silent no-op.
- The empty text gate `exit.map11.40.w` is a real source gap (`Text: `), not an alias to guess.
- ROOMCOMMANDS lists gates, materialized room objects, executable room scripts, and live NPC-specific ASK routes. The client runs a selected entry through the ordinary server command path.
