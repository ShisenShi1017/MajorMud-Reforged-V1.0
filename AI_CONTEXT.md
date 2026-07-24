# AI Context for Reforged

Read `AGENTS.md` first, then read this file, then read `Docs/PROJECT_STATUS.md`.

`Docs/PROJECT_STATUS.md` is the concise live handoff. Old duplicate CodeUpdate handoff/changelog/manifest doc trees were removed to keep the repository readable.

## Current Position

- Current Code Update: `0139`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0139 - Telnet Runtime Cohesion`.
- Current local status: runtime cohesion work applied locally; not pushed by Codex.
- CodeUpdate0138: pushed and green by user report.
- Latest local verification for CodeUpdate0139: Bundle H final audit sweep PASS 63/63 with raw logs under `Reports/BundleH/FinalGate/`; all resumed final smoke segments PASS after the item-runtime, live-gameplay, living-world, and player-combat-profile repairs; solution Release build PASS with warnings-as-errors; compiler tests PASS; core/server smoke tests PASS; server and app win-x64 publish PASS; published `Reforged.Server.exe` PASS for Classic data audit, item runtime, live gameplay, living-world, player combat profile, and telnet listener startup on `127.0.0.1:4000`; published `MajorMudReforged.exe` PASS for shell/settings/cleanup smoke routes and brief GUI launch/startup check after desktop logger hardening. GitHub Actions pending until pushed and confirmed green. Python validator could not be rerun in this Codex process because `python`/`py` are not accessible.
- Decode Exhaustion Bundle F Revision 0033 and Bundle G Revision 0002 are installed locally into the dirty CodeUpdate0139 repo without commit or push. Bundle G closes the prior F10 native payload gap: server Release build passes, `--audit-bundle-f-phase-f10-native-payloads` now reports 0 disconnects, and Bundle F F10 complete through F15 audits pass locally.
- GitHub Actions is pending for CodeUpdate0139 until the user pushes and reports green.

## Non-Negotiable Rules

- Librarium is the authoritative gameplay source.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Preserve real `Saves/` and `ImportedDatabases/`.
- Do not commit smoke-save artifacts.
- Do not move to the next public update until CodeUpdate0139 is pushed and GitHub Actions is green, unless the user explicitly overrides the gate.

## CodeUpdate0139 Local Summary

- Telnet sessions continue to use the authoritative `RuntimeCommandDispatcher`.
- Room rendering already uses live `RuntimeLivingWorldService` NPC instances, players, ground items, exit state, and stat line.
- Combat targeting now resolves against live NPC instances visible in the room instead of static spawn definitions.
- Live combat rounds now refresh target HP from the shared living-world instance and apply NPC damage there.
- Spell and ability NPC target resolution now accepts live NPC instance, spawn, NPC ID, and name matches in the room.
- Spell and ability NPC damage/healing now mutates shared living-world state and keeps active combat encounter HP synchronized.
- Movement now calls the live combat movement guard before traversal, preventing walk-away movement during active combat.
- Bundle 3 adds Classic runtime audit tooling for compiler output, runtime load counts, room inspection, and spawn verification.
- Audit result: regenerated `Data/Classic/rooms.json` contains 26,668 rooms with descriptions and 0 rooms without descriptions after the WCCMP002 placeholder recovery.
- Audit result: runtime/live-world materialization currently creates 1,015 live NPC instances and 628 live monster instances after hard map-qualified placement validation, so resolved spawns instantiate without global/group leakage.
- Room-text recovery adds a compiler-side native map DAT resolver plus `audit-room-text` and `export-room-text` commands.
- Librarium now contains 2,417 binary-backed room-description records in `Librarium/Objects/room_descriptions/native_map_room_description_records.json`.
- A second lower-priority room-description file, `Librarium/Objects/room_descriptions/reference_room_description_records.json`, adds 800 OmniVerse map-qualified reference gap-fill descriptions for canonical rooms not already covered by native evidence.
- `Librarium/Objects/room_descriptions/wccmp002_room_description_records.json` adds 23,309 WCC default-install binary-backed gap-fill descriptions from `ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat`.
- `Librarium/Objects/room_descriptions/wccmp002_placeholder_room_description_records.json` adds the final 140 placeholder-named room descriptions. For this pass, WCCMP002 is treated as the selected MajorMUD 1.11p room/map source and WBBMP002 is secondary corroboration only.
- Compiler room-text projection now merges all Librarium room-description files and preserves native/binary priority over reference-backed gap fills.
- The read-only WCC Librarium exhaustion audit reports 2,417/26,668 active room-description coverage, one clean promotable WCCMP map file, one parser-blocked reference-only map file, and 15 locally missing expected WCCMP map files.
- MMExplorer SQL/MDB and OmniVerse public codex reconciliation added reference-backed gap-fill data into Librarium:
  - matched items/spells/NPCs/shops received `public_reference` and `mmexplorer_sql_reference`;
  - unmatched MMExplorer rows are retained as supplement files under the relevant family;
  - OmniVerse ability records created the new `Librarium/Objects/abilities/ability_records.json` family.
- Latest reconciliation report: `Reports/DataDecode/mmexplorer_omniverse_librarium_reconciliation.md`.
- Live `Data/Classic` was regenerated from Librarium after the room-description promotion; server audit now reports 26,668 rooms with descriptions and 0 rooms without descriptions.
- Compiler smoke output at `Data/CompilerSmokeTest/ClassicRoomDescriptionComplete0139` proves 26,668/26,668 generated room descriptions.
- WCCMP002 placeholder recovery promoted the last 140 placeholder-named rooms, with WBBMP002 title and first-line corroboration hits for all 140 records.
- Command parsing now uses a central registry for exact aliases, unique-prefix abbreviations, and ambiguity rejection for unsafe prefixes such as `s`, `se`, and `st`.
- Live spawn placement now requires exact map-qualified `(map_id, room_number)` resolution and no longer falls back to legacy numeric room IDs, the player room, start room, or a shared/default bucket.
- Grouped spawn candidates no longer materialize as every possible NPC in the room. The runtime uses explicit generated room NPC definitions when present and records unresolved placement failures instead of attaching spawns globally.
- `audit room` now proves each displayed entity's definition ID, spawn ID, source room reference, resolved runtime room, live `RoomId`, alignment, disposition, and combat hostility state.
- `room.map1.21` placement audit currently shows one neutral `guardsman` (`npc.14`) from `spawn.21.5`; storm giants, woodelf entries, bosses, and unrelated group candidates are not visible. The user-expected `elite guardsman` is not supported by the current generated source field `(Max 1): 14,[5-1-1-1]` and needs a separate evidence-backed data correction if wrong.
- Hard spawn placement validation currently skips two classified invalid/non-spawn records: `spawn.288.43` points to non-existent `room.map1.288`, and `spawn.2306.213` points to `room.map1.2306`, which the user verified is a shop with no NPC.
- Current Classic generated counts after the folded material: 26,668 rooms, 61,976 exits, 291 room interactions, 367 triggers, 183 scripts, and 2,183 quests.
- Room native/passive effect closure resolved the remaining 7 distinct native/passive room-effect linkage gaps across 12 generated exit rows. The affected exits now compile with `native_passive_effect_resolved`, `resolved_without_player_command`, and `player_command_promoted=false`.
- MegaMUD `/mp` route replay was used against imported route files for those exact map-qualified exits. It found 0 command hits, so no player command binding was invented or promoted.
- Native WCCMSG room-message strings are cataloged as stable offset IDs in `Librarium/Objects/room_messages/room_message_records_catalog.json` and generated to `Data/Classic/room_messages.json`.
- Room message categories now cover room action aliases/success text, door opens, hidden exit opens/traversal, lock/key failures, search successes, and generic failures.
- Room native effect/message decode report: `Reports/DataDecode/room_native_effect_linkage_and_message_decode.md`.
- Quest dialogue dispatch intake loaded `MajorMUD_Reforged_Cumulative_Update_0206.zip` into Librarium source records instead of treating package `/Data` as authority.
- `Librarium/Objects/scripts/mmexplorer_textblock_stage_records.json` now provides 183 generated script records, including 182 decoded `mmexplorer_textblock_stage` scripts.
- `Librarium/Objects/triggers/mmexplorer_room_interaction_records.json` now merges the 284 room/exit interaction triggers with 83 NPC quest-entry triggers and 182 linked stage script references.
- Runtime `talk <npc>` dispatch now resolves room-scoped live NPCs, binds NPC quest-entry triggers, evaluates linked stage scripts in decoded stage order, applies decoded flags/items/experience/alignment atomically, emits text/message branch IDs, and fails closed without mutation when conditions or handlers do not resolve.
- Quest state commands now expose generated `journal`, `quest`, `accept`, `abandon`, `complete`, and `quest reset` state markers while decoded NPC dialogue remains the authoritative mutation path.
- Generated random textblock tables now execute through decoded independent 1..100 threshold semantics.
- Generated quest cast/summon/teleport/remoteaction operations now validate generated targets before commit.
- Quest teleport intents update the active session and character room; summon intents materialize generated NPCs into the living world.
- Quest remoteaction intents are recorded into the living-world event journal with exact decoded metadata and an explicit evidence-gated marker instead of guessed native mutation.
- Room/object interaction commits now publish living-world `world-object-interacted` events after state persistence.
- Final native closure/WCC decode material from the user archive is now in the repo: 1,673 new files copied, 1 identical file skipped, and the user-confirmed server runtime slices were merged into active runtime code. Duplicate runtime contracts were renamed or consolidated so the runtime compiles without parallel definitions.
- Intake report: `Reports/PackageIntake/final_native_closure_update0022_intake.json`.
- Verified/native and strongly-correlated Librarium records are now active in executable generated data where evidence supports it. `room_interactions` compiles from the base source plus strongly-correlated sidecars, generated `triggers` has 367 strongly-correlated records including 83 NPC quest-entry triggers, and unresolved exit targets are compiler-blocked instead of being emitted as active traversal.
- Bundle F local repair work fixed current decoded-description projection, exact NPC corpse gold/id transfer, non-stackable item smoke assumptions, graph traversal for presentation-only text/message operations, variable operand role projection, Witchhunter ability mapping, optional world reward activation terms, nullable NPC dialogue names, and current NPC dialogue execution audit counts.
- Bundle G Revision 0002 adds native presentation mapping evidence for decoded graph-only no-output text nodes and exact WCCMSG dialogue message payloads. `message 2044` resolves to the nekojin elder copper amulet message, `message 3290` resolves to the Dhelvanen portal message, unknown message IDs still fail closed, and no fallback text substitution is allowed.
- Bundle H baseline report: `Reports/BundleH/BASELINE_RUNTIME_CONSOLIDATION_AUDIT.md`. Safe repairs added `search` to the supported runtime command set, corrected Bundle D search audit semantics to reroll/replace transient search results, scoped the Bundle D door audit to door/object validation, adjusted Bundle A repository-coverage gating to separate true generated-unconsumed fields from evidence-name-only warnings, and promoted the two imported WCC room-anchor environment records that were absent from the MMExplorer-derived environment catalog.
- Bundle A character audit now passes with 13 races, 15 classes, no generated-unconsumed Bundle A gameplay fields, and 57 evidence-name-only warnings recorded separately.
- Bundle D final now passes locally with `AuditPassed=true` and `BundleDComplete=true`. D9 now reports 26,668 runtime rooms and 26,668 decoded environment records. `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` are backed by imported WCC room records and source-classified as `decoded_imported_wcc_room_record_anchor`. D9 explicitly asserts both anchor records and their decoded values. Neither room is a gang house, so gang-house environment rows remain 0.
- Bundle E zero-disconnect audit now passes locally with `BundleEComplete=true`, 1,100 NPC definitions, 962 spawn records, all 13 NPC/creature subsystems passed, and no duplicate authoritative creature source paths. Focused companion, merchant/trainer, boss/death/respawn, NPC item/control/persistence, and relationship/service closure audits also pass.
- Bundle H current gate logs are preserved under `Reports/BundleH/CurrentGate/`; the compiler build, server build, seven focused runtime audits, and the gang-house service-location audit pass locally.
- Bundle H final gate logs are preserved under `Reports/BundleH/FinalGate/`. Final local repairs: bank vault item location validation now allows bank IDs while retaining owner identity; owner-limited item smoke uses isolated state; `ba` is an exact `bash` alias while `bs` remains `backstab`; living-world smoke now reports generated gate kinds and classified validation signals against current 26,668-room / 367-interaction data; player combat profile smoke separates stored attack/defense checks from derived backstab and quest/dodge formula consumers.
- Bundle H gang-house service-location audit is recorded at `Reports/BundleH/CurrentGate/bundle_h_gang_house_audit.json`. Native/default WCC gang files, ten MudInfo gang-house maps, ten deed items, 22 gang-house/deed shops, and deed/upkeep cleanup protection are verified. Librarium now contains `Librarium/Objects/gang_houses/gang_house_records.json`, generated Classic data contains `Data/Classic/gang_houses.json`, and runtime consumes the generated catalog through `RuntimeClassicGangHouseCatalog` and `RuntimeClassicGangHouseProtectionService`.
- Gang-house service-location scope is 10 Classic houses, 20 WCC shop API-backed service locations, 10 vault rooms, and 10 supply-shop rooms; all promoted locations resolve to runtime rooms and current/expired/non-gang-room protection checks pass. Normal interior house ownership coverage and live deed acquisition/upkeep mutation remain blocked until exact evidence is recovered. `Data/Classic/environment_rooms.json` still has 0 native gang-house flags, MudInfo maps remain reference-only, and `LibrariumLocal/core_graph.json` plus `LibrariumLocal/runtime_index.json` are absent from the repo.
- The Reforged app now has a real read-only Librarium browser tab with Weapons, Armor, Spells, Abilities, Classes, Races, Items, Monsters, Quests, and Rooms categories, selected-tab search, name-only left-side record lists, character-foundation class/race gameplay details, and nontechnical quest guidance showing who to talk to, what to say, needed items, and plain-language steps. The parchment tab/list/grid text is dark and readable.
- The Reforged app Client play flow now matches the intended MegaMUD-style direction: Play is a server/session launcher, actual gameplay opens in a dedicated black terminal window, and client tools/settings are hidden behind menus or a separate tabbed options window.
- Telnet character creation now requires a Male/Female selection before the character build is finalized. The selected sex is persisted on `RuntimeCharacterRecord`, existing saves default to `Unspecified`, and character menus display the value when present.
- Compiler room-description loading now accepts both `*_records.json` and older `*_records_*.json` Librarium room-description files, allowing the WCCMP002 1.11p description corpus to project into generated `Data/Classic`.
- Runtime quest dialogue and item catalog readers now accept integer-compatible numeric strings from generated Librarium data for quest arguments and item weights/fields; this fixed the Telnet/server smoke crash that reported a JSON `String` where a `Number` was expected.
- Current runtime audit counts after the repair: 26,668 rooms loaded, 26,668 room descriptions loaded, 1,100 NPC definitions, 1,078 monster definitions, 962 spawn records, 1,015 live NPC instances, 628 live monster instances, 1,648 doors, 178 shops, 27 banks, 44 trainers, 2,183 quests, and 367 triggers.
- Remaining native decode report: `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md`.
- Classic completed quests cannot be repeated on the same character. Replaying a completed quest requires starting a new character; same-character quest reset/restart must remain blocked for Classic.
- Validator now includes `server_interaction_narrative`; latest pass verifies 83 executable quests, 182 stage scripts, 83 NPC quest-entry triggers, item consumption/reward transition, dialogue branch dispatch, random table execution, cast/summon/remoteaction coverage, remoteaction event recording, room/object interaction event recording, world-intent target validation, failure/no-mutation behavior, and exhaustive NPC quest-entry dispatch validation.
- Latest audit report: `Reports/RuntimeVerification/classic_runtime_audit.md`.
- Latest validator report: `Reports/RuntimeVerification/validator_latest.md`.
- `Saves/` and `ImportedDatabases/` remain untouched.

## Next Gate

1. Review the CodeUpdate0139 local runtime cohesion diff.
2. Review the Librarium room-description promotion proof and regenerated `Data/Classic` output.
3. Push only when approved by the user.
4. Wait for GitHub Actions green on the pushed commit before beginning the next public update.

## Startup Order

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/PROJECT_STATUS.md`
4. `Docs/Architecture/PROJECT_ROADMAP.md`
5. `README.md`
6. `VERSION.md`
7. `CHANGELOG.md`
8. `ProjectManifest.json`
9. `DEVELOPMENT_PRINCIPLES.md`
10. Relevant source, generated data, validation, and report files

One-time validation proofs are recorded in ValidationManifest.json.
