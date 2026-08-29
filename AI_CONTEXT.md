# AI Context for Reforged

- Current milestone: **CodeUpdate0140 - Classic Parity Closure and Creator Studio**.
- Current parity: **98 PASS / 102 STRONG / 0 UNKNOWN = 200** (**49.0% strict / 100.0% evidence-backed**).
- The exact original WCCMMUD 1.11p DLL plus wccknms2.dat trace proves both Nahr message-8480 caller arguments are empty strings for all five attacks. Preserve the native empty spacing.
- Librarium/Parity/majormud_parity_assertions.json is the cumulative retained ledger. ValidationManifest.json records accepted one-time validation.
- Tools/validator.py is the only current-health entrypoint. Runtime parity closure uses --smoke-classic-parity-closure.
- Historical pass-numbered validators, scorecards, ledgers, validation reports, and runtime smoke aliases are retired. Distinct production authority classes remain separate by subsystem.
- Creator Studio full-control Custom1 authoring is integrated. Classic remains immutable and evidence-locked.
- Generated Data is untracked. Local Borin state, saves, imported-data archives, and release output are preserved and excluded from this cleanup commit.
- GitHub Actions is the official gate after push.

## Current Position

- Current Code Update: `0140`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0140 - Repository Cleanup and Imported Evidence Archive`.
- Current local status: CodeUpdate0139 runtime cohesion is closed locally; CodeUpdate0140 cleanup/archive baseline is being prepared for push.
- CodeUpdate0138: pushed and green by user report.
- Latest local verification for CodeUpdate0140: CodeUpdate0139 Bundle H final audit sweep PASS 63/63 and final smoke sweep PASS are retained as the runtime baseline. After imported/reference archival cleanup, `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore` passes with 0 warnings/errors and `python Tools\validator.py` passes 15/15 current-health checks. GitHub Actions pending until pushed and confirmed green.
- Decode Exhaustion Bundle F Revision 0033 and Bundle G Revision 0002 are installed locally into the dirty CodeUpdate0139 repo without commit or push. Bundle G closes the prior F10 native payload gap: server Release build passes, `--audit-bundle-f-phase-f10-native-payloads` now reports 0 disconnects, and Bundle F F10 complete through F15 audits pass locally.
- Native presentation regeneration is reproducible: 3,467 verified WCCTEXT2 records are canonical Librarium data and compile into one generated runtime file. Clean Classic compile reports 30 families, 101,731 objects, and 35 files.
- GitHub Actions is pending for CodeUpdate0140 until the pushed commit is confirmed green.
- Guardian Hall now includes the ten-phase Borin engineering department with isolated workspaces, guarded task execution, persistent engineering memory/reports/statistics, and independent persisted switches for planning, engineering, build, validation, runtime testing, gameplay testing, optimization, documentation, candidate ZIPs, repository updates, and overnight operation. Candidate ZIP generation defaults on; all autonomous and repository-update switches default off.
- Guardian Hall Engineering includes a persisted objective field and a one-click `Fix Runtime Disconnects` assignment that creates capability-gated high-priority work.
- Guardian Hall Autonomous Engineering is connected to The Forge Master Brain: it supplies captured diagnostics/task history, requires a fenced unified diff, validates/applies the patch only in the isolated worktree, and fails visibly when LM Studio or a selected model is unavailable.
- Engineering workspace creation resolves the repository through The Forge repository service; it does not derive Git paths from Guardian Hall's AppData state directory.
- Guardian Hall and the full solution compile in Release with warnings-as-errors after connecting generated shop markup to the existing native economy markup authority and resolving strict nullable diagnostics.

## Non-Negotiable Rules

- Librarium is the authoritative gameplay source.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Preserve real `Saves/`. Imported/reference databases and raw evidence are retained locally in `Archive/ImportedDatabasesAndEvidence.LocalOnly.rar`, not as committed source.
- Runtime saves are realm-scoped: Classic uses `Saves/Classic`, custom worlds use `Saves/Custom/<realm>`, host account credentials use `Saves/Host`, and one login may have up to three characters per world.
- Do not commit smoke-save artifacts.
- Do not move to the next public update until CodeUpdate0140 is pushed and GitHub Actions is green, unless the user explicitly overrides the gate.

## CodeUpdate0139 Runtime Summary

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
- Decoded exit-action closure binds the exact stone-sphinx answers for Great Pyramid rooms 12/1920 and 12/2001 plus the exact randomized `testskill strength 0 1423` rubble chain for room 9/1012.
- Ten Map 17 boulder-side exits execute exact decoded `move|push boulder`, five-second delay metadata, `testskill strength 60 4235`, and remote actions.
- Four decoded Map 17 pedestal scripts consume `item.1921`/`1923`/`1922`/`1924` and bind the four remote-action steps for `exit.map17.3042.n`.
- NPC `550` owns the exact `stars`/`star`/`the stars` command chain through textblocks 2530/2531/2532 to `remoteaction 2051 0 0 8`. `exit.map12.2051.u` is now bound and the living-world audit reports 206/206 action gates with zero native/passive placeholders.
- Static room-item reconciliation promotes and materializes 363 shared-world records across 287 rooms: 102 visible and 261 hidden-searchable. Room rendering, pickup, visible re-drop, Search, and living-world persistence are connected; eight unsafe rows remain excluded.
- Static placement reset restores missing ordinary items exactly once at the host maintenance boundary even when a player owns another copy, blocks only when the decoded positive item `game_limit` is reached across world and externally owned quantities, and is replay-idempotent. Scheduling remains external because no decoded universal wall-clock interval or placement-specific timer link is proven.
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
- Latest reconciliation report: `Reports/DataDecode/decoded_data_evidence_summary.md`.
- Live `Data/Classic` was regenerated from Librarium after the room-description promotion; server audit now reports 26,668 rooms with descriptions and 0 rooms without descriptions.
- Compiler smoke output at `Data/CompilerSmokeTest/ClassicRoomDescriptionComplete0139` proves 26,668/26,668 generated room descriptions.
- WCCMP002 placeholder recovery promoted the last 140 placeholder-named rooms, with WBBMP002 title and first-line corroboration hits for all 140 records.
- Command parsing now uses a central registry for exact aliases, unique-prefix abbreviations, and ambiguity rejection for unsafe prefixes such as `s`, `se`, and `st`.
- Live spawn placement now requires exact map-qualified `(map_id, room_number)` resolution and no longer falls back to legacy numeric room IDs, the player room, start room, or a shared/default bucket.
- Grouped spawn candidates no longer materialize as every possible NPC in the room. The runtime uses explicit generated room NPC definitions when present and records unresolved placement failures instead of attaching spawns globally.
- `audit room` now proves each displayed entity's definition ID, spawn ID, source room reference, resolved runtime room, live `RoomId`, alignment, disposition, and combat hostility state.
- `room.map1.21` placement audit currently shows one neutral `guardsman` (`npc.14`) from `spawn.21.5`; storm giants, woodelf entries, bosses, and unrelated group candidates are not visible. The user-expected `elite guardsman` is not supported by the current generated source field `(Max 1): 14,[5-1-1-1]` and needs a separate evidence-backed data correction if wrong.
- Hard spawn placement validation currently skips two classified invalid/non-spawn records: `spawn.288.43` points to non-existent `room.map1.288`, and `spawn.2306.213` points to `room.map1.2306`, which the user verified is a shop with no NPC.
- Current compact Classic generated counts: 26,668 rooms, 61,976 exits, 397 room interactions, 83 live quest-entry triggers, 182 executable stage scripts, and 83 executable quests. Pending quest candidates, formula probes, duplicate room/exit analysis triggers, and the tooling script catalog remain Librarium-only.
- Room native/passive effect closure resolved the remaining 7 distinct native/passive room-effect linkage gaps across 12 generated exit rows. The affected exits now compile with `native_passive_effect_resolved`, `resolved_without_player_command`, and `player_command_promoted=false`.
- MegaMUD `/mp` route replay was used against imported route files for those exact map-qualified exits. It found 0 command hits, so no player command binding was invented or promoted.
- Native WCCMSG room-message strings remain cataloged as stable offset evidence in `Librarium/Objects/room_messages/room_message_records_catalog.json`; the redundant unconsumed runtime file is retired after exact comparison with executable interactions and native payloads.
- Room message categories now cover room action aliases/success text, door opens, hidden exit opens/traversal, lock/key failures, search successes, and generic failures.
- Room native effect/message decode report: `Reports/DataDecode/decoded_data_evidence_summary.md`.
- Quest dialogue dispatch intake loaded `MajorMUD_Reforged_Cumulative_Update_0206.zip` into Librarium source records instead of treating package `/Data` as authority.
- `Librarium/Objects/scripts/mmexplorer_textblock_stage_records.json` now provides 183 generated script records, including 182 decoded `mmexplorer_textblock_stage` scripts.
- `Librarium/Objects/triggers/mmexplorer_room_interaction_records.json` now merges the 284 room/exit interaction triggers with 83 NPC quest-entry triggers and 182 linked stage script references.
- Runtime `talk <npc>` dispatch now resolves room-scoped live NPCs, binds NPC quest-entry triggers, evaluates linked stage scripts in decoded stage order, applies decoded flags/items/experience/alignment atomically, emits text/message branch IDs, and fails closed without mutation when conditions or handlers do not resolve.
- Quest state commands now expose generated `journal`, `quest`, `accept`, `abandon`, `complete`, and `quest reset` state markers while decoded NPC dialogue remains the authoritative mutation path.
- Generated random textblock tables now execute through decoded independent 1..100 threshold semantics.
- Generated quest cast/summon/teleport/remoteaction operations now validate generated targets before commit.
- Quest teleport intents update the active session and character room; summon intents materialize generated NPCs into the living world.
- Quest remoteaction closure now reports the single generated quest-stage signature `1423.66.0.3` as executable with zero evidence-gated signatures. Native parser/callee analysis corrected the arguments to action-record/message/selector/action-slot; WCCMP002 proves slot 3 in room `9/1423` and paired slot 2 in room `9/1425` are reciprocal type-7/state-2 actions. The live host toggles both door states, persists them, and journals the native result state.
- Room/object interaction commits now publish living-world `world-object-interacted` events after state persistence.
- Final native closure/WCC decode material from the user archive is now in the repo: 1,673 new files copied, 1 identical file skipped, and the user-confirmed server runtime slices were merged into active runtime code. Duplicate runtime contracts were renamed or consolidated so the runtime compiles without parallel definitions.
- Intake report: `Reports/PackageIntake/final_native_closure_update0022_intake.json`.
- Verified/native and strongly-correlated Librarium records are now active in executable generated data where evidence supports it. `room_interactions` compiles from the base source plus strongly-correlated sidecars, generated `triggers` has 367 strongly-correlated records including 83 NPC quest-entry triggers, and unresolved exit targets are compiler-blocked instead of being emitted as active traversal.
- Bundle F local repair work fixed current decoded-description projection, exact NPC corpse gold/id transfer, non-stackable item smoke assumptions, graph traversal for presentation-only text/message operations, variable operand role projection, Witchhunter ability mapping, optional world reward activation terms, nullable NPC dialogue names, and current NPC dialogue execution audit counts.
- Bundle G Revision 0002 adds native presentation mapping evidence for decoded graph-only no-output text nodes and exact WCCMSG dialogue message payloads. `message 2044` resolves to the nekojin elder copper amulet message, `message 3290` resolves to the Dhelvanen portal message, unknown message IDs still fail closed, and no fallback text substitution is allowed.
- Bundle H baseline is summarized in continuity documentation; historical raw logs are retired during repository cleanup. Safe repairs added `search` to the supported runtime command set, corrected Bundle D search audit semantics to reroll/replace transient search results, scoped the Bundle D door audit to door/object validation, adjusted Bundle A repository-coverage gating to separate true generated-unconsumed fields from evidence-name-only warnings, and promoted the two imported WCC room-anchor environment records that were absent from the MMExplorer-derived environment catalog.
- Bundle A character audit now passes with 13 races, 15 classes, no generated-unconsumed Bundle A gameplay fields, and 57 evidence-name-only warnings recorded separately.
- Bundle D final now passes locally with `AuditPassed=true` and `BundleDComplete=true`. D9 now reports 26,668 runtime rooms and 26,668 decoded environment records. `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` are backed by imported WCC room records and source-classified as `decoded_imported_wcc_room_record_anchor`. D9 explicitly asserts both anchor records and their decoded values. Neither room is a gang house, so gang-house environment rows remain 0.
- Bundle E zero-disconnect audit now passes locally with `BundleEComplete=true`, 1,100 NPC definitions, 962 spawn records, all 13 NPC/creature subsystems passed, and no duplicate authoritative creature source paths. Focused companion, merchant/trainer, boss/death/respawn, NPC item/control/persistence, and relationship/service closure audits also pass.
- Bundle H current gate summary is preserved in continuity documentation; historical raw logs are retired during repository cleanup; the compiler build, server build, seven focused runtime audits, and the gang-house service-location audit pass locally.
- Bundle H final gate is summarized in continuity documentation; historical raw logs are retired during repository cleanup. Final local repairs: bank vault item location validation now allows bank IDs while retaining owner identity; owner-limited item smoke uses isolated state; `ba` is an exact `bash` alias while `bs` remains `backstab`; living-world smoke now reports generated gate kinds and classified validation signals against current 26,668-room / 367-interaction data; player combat profile smoke separates stored attack/defense checks from derived backstab and quest/dodge formula consumers.
- Bundle H gang-house service-location audit is summarized in continuity documentation. Native/default WCC gang files, ten MudInfo gang-house maps, ten deed items, 22 gang-house/deed shops, and deed/upkeep cleanup protection are verified. Librarium now contains `Librarium/Objects/gang_houses/gang_house_records.json`, generated Classic data contains `Data/Classic/gang_houses.json`, and runtime consumes the generated catalog through `RuntimeClassicGangHouseCatalog` and `RuntimeClassicGangHouseProtectionService`.
- Gang-house service-location scope is 10 Classic houses, 20 WCC shop API-backed service locations, 10 vault rooms, and 10 supply-shop rooms; all promoted locations resolve to runtime rooms and current/expired/non-gang-room protection checks pass. Normal interior house ownership coverage and live deed acquisition/upkeep mutation remain blocked until exact evidence is recovered. `Data/Classic/environment_rooms.json` still has 0 native gang-house flags, MudInfo maps remain reference-only, and `LibrariumLocal/core_graph.json` plus `LibrariumLocal/runtime_index.json` are absent from the repo.
- The Reforged app now has a real read-only Librarium browser tab with Weapons, Armor, Spells, Abilities, Classes, Races, Items, Monsters, Quests, and Rooms categories, selected-tab search, name-only left-side record lists, character-foundation class/race gameplay details, and nontechnical quest guidance showing who to talk to, what to say, needed items, and plain-language steps. The parchment tab/list/grid text is dark and readable.
- The Reforged app Client play flow now matches the intended MegaMUD-style direction: Play is a server/session launcher, actual gameplay opens in a dedicated black terminal window, and client tools/settings are hidden behind menus or a separate tabbed options window.
- Telnet character creation now requires a Male/Female selection before the character build is finalized. The selected sex is persisted on `RuntimeCharacterRecord`, existing saves default to `Unspecified`, and character menus display the value when present.
- Compiler room-description loading now accepts both `*_records.json` and older `*_records_*.json` Librarium room-description files, allowing the WCCMP002 1.11p description corpus to project into generated `Data/Classic`.
- Runtime quest dialogue and item catalog readers now accept integer-compatible numeric strings from generated Librarium data for quest arguments and item weights/fields; this fixed the Telnet/server smoke crash that reported a JSON `String` where a `Number` was expected.
- Current runtime audit counts after compaction: 26,668 rooms loaded, 26,668 room descriptions loaded, 1,100 NPC definitions, 1,078 monster definitions, 962 spawn records, 1,015 live NPC instances, 628 live monster instances, 1,648 doors, 178 shops, 27 banks, 44 trainers, 83 executable quests, 83 live quest-entry triggers, and 182 stage scripts.
- Remaining native decode report: `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md`.
- Classic completed quests cannot be repeated on the same character. Replaying a completed quest requires starting a new character; same-character quest reset/restart must remain blocked for Classic.
- Validator now includes `server_interaction_narrative`; latest pass verifies 83 executable quests, 182 stage scripts, 83 NPC quest-entry triggers, item consumption/reward transition, dialogue branch dispatch, random table execution, cast/summon/remoteaction coverage, remoteaction event recording, room/object interaction event recording, world-intent target validation, failure/no-mutation behavior, and exhaustive NPC quest-entry dispatch validation.
- Latest audit report: `Reports/RuntimeVerification/classic_runtime_audit.md`.
- Latest validator report: `Reports/RuntimeVerification/validator_latest.md`.
- `Saves/` remains untouched. Imported/reference databases and raw evidence are local-only archival material after the approved cleanup.

## Next Gate

1. Push CodeUpdate0140 and wait for green GitHub Actions.
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
## CodeUpdate0139 WCCMMUD SYSOP closure

The 20 decoded WCCMMUD SYSOP/GAMEOP roots now compile from Librarium into generated Classic data and are consumed by authenticated runtime dispatch. Moderator receives only the ten decoded GAMEOP-allowed roots; Sysop and Administrator receive the full catalog. `BUFFERS`, `STATUS`, `MAP`, room `RESET`, realm `SPEAK`, `ARENA`, and all six named GOTO destinations are executable. The six Support Chamber invasion phrases execute the exact 25 placement scripts and six NPC-mixture tables recovered from WCCUPDA2; they require SYSOP/Administrator authority, consume their decoded cycle gate, summon through the living world, and are cleared/reset by board cleanup. Gang commands and other missing owner services fail closed. Focused SYSOP/invasion verification passes 15/15.

The follow-up evidence pass makes `GOTO NewHaven` (`room.map1.2150`) and `GOTO Support` (`room.map1.164`) executable and persistent. Textblock 2454 contains complete native scripts for orc, cult, red-dragon, green-dragon, duergar, and saracen invasions; execution is not yet live because that payload is not projected as a room interaction.
Latest local continuation: grouped ability pass 123-124 verifies HPRegen on 25 items and 16 spells through the live regeneration cadence. NegateAbility consumes all five spell and two NPC owners; positive target IDs suppress only that decoded ability, both Tyrannosaurs reject Confusion, and the zero-valued hold-immunity record remains fail-closed. Release server build, item smoke, and Bundle B combat audit pass.
Latest local continuation: abilities 125-134 are explicitly modeled as the ten decoded Classic quest-progression state fields. Exact identity, numeric persistence, provenance, and exclusion from player power restoration are regression-locked. Character Bundle A and interaction/narrative smoke pass.
