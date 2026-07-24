# Changelog

## Guardian Hall Phase 3-0164 — Final Hardening and Self-Test

- Added atomic Guardian Hall state writes through a temporary file and replace operation.
- Added a recoverable `.backup` state snapshot before every replacement.
- Added automatic fallback to the backup when the primary state JSON is malformed.
- Advanced Guardian Hall persistence to schema version 7.
- Added a persisted diagnostics workspace and an in-application **Run Hall Self-Test** control.
- Added five-role smoke validation, Wanderer character retention, Worldsmith Live Realm isolation, Archivist preview-only enforcement, scheduler safety, connector URL/timeout bounds, and atomic save/reload verification.
- Added persisted pass/fail results to the Guardian Activity Center.
- Added Phase 3-0164 static validation and updated the Phase 3-0161 validator to accept later compatible schema versions.
- Release compilation remains pending in environments without the .NET 8 SDK.


## CodeUpdate0139 - Telnet Runtime Cohesion

### GitHub Actions Generated Data Bootstrap Repair

- Kept generated `Data/Classic` out of Git tracking; Librarium remains the committed gameplay authority.
- Updated the GitHub Actions server workflow to restore/build the Librarium compiler and regenerate Classic runtime data before server bootstrap diagnostics.
- Added compiler/runtime chunked-record support for oversized Librarium source catalogs while keeping generated `/Data` local.
- Added the missing Librarium `textblock_command_ability_contracts` source family and allowed NPC AI relationship derivation from generated spawns when the optional derivative `npc_ai_runtime.json` is absent.

### Bundle H Final Gate Repair

- Repaired runtime item ownership validation so bank vault location IDs remain valid while owner identity is still enforced.
- Isolated owner-limited item smoke state so earlier ordinary item checks do not contaminate limited-item proof.
- Added exact `ba` alias routing to `bash` while preserving `bs` for `backstab`.
- Updated living-world smoke to validate against generated data counts and report actual gate kinds / validation signals.
- Split player combat profile smoke checks between stored character fields and derived backstab/quest/dodge formula consumers.
- Hardened the desktop app logger so a temporarily locked `application.log` cannot crash startup or shutdown.
- Added startup failure logging for the WPF startup path before the user-facing error dialog is shown.

Verification:

- `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore -warnaserror` PASS.
- `dotnet test Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj --configuration Release --no-build` PASS.
- `dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build` PASS.
- Bundle H final audit sweep PASS 63/63.
- Final smoke sweep PASS across all resumed segments.
- Published `Reforged.Server.exe` PASS for Classic data audit, item runtime, live gameplay, living-world, player combat profile, and telnet listener startup on `127.0.0.1:4000`.
- Published `MajorMudReforged.exe` PASS for shell/settings/cleanup smoke routes and brief GUI launch/startup check.

Known limitation:

- Python validator could not be rerun in this Codex process because `python`/`py` are not accessible.
- GitHub Actions remains pending until CodeUpdate0139 is pushed and confirmed green.

- Added Bundle H baseline runtime consolidation report at `Reports/BundleH/BASELINE_RUNTIME_CONSOLIDATION_AUDIT.md`.
- Added `search` to the supported runtime command catalog; dispatch was already routed to the live Classic search handler.
- Updated Bundle D search audit semantics to verify Classic transient search reroll/replace behavior instead of the older persisted-discovery expectation.
- Scoped Bundle D door audit validation to door/world-object failures so unrelated living-world categories do not fail the door gate.
- Verified server build PASS with warnings-as-errors after the Bundle H audit repairs.
- Verified Bundle D search, door, and corrective audits PASS after the repairs.
- Verified Bundle A character audit PASS after separating true generated-unconsumed fields from repository-wide evidence-name-only warnings.
- Promoted imported WCC room-anchor environment records for `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` into generated Classic environment data; both were absent only from the MMExplorer-derived environment catalog.
- Updated Bundle D environment audit to require one decoded map-qualified environment row per runtime room and to require gang-house runtime consumption to match the decoded catalog instead of inventing nonzero gang-house rows.
- Added explicit D9 regression checks for the two imported WCC environment anchors and their decoded values.
- Verified Bundle D environment audit PASS with 26,668 runtime rooms, 26,668 decoded environment records, and 0 decoded gang-house environment rows.
- Verified Bundle D final PASS with `AuditPassed=true` and `BundleDComplete=true`.
- Verified Bundle E focused closure audits PASS for companion lifecycle, merchant/trainer, boss/death/respawn, NPC item/control/persistence, and relationship/service.
- Verified `--audit-bundle-e-zero-disconnects` PASS with `BundleEComplete=true`, 1,100 NPC definitions, 962 spawn records, all 13 NPC/creature subsystems passed, and no duplicate authoritative creature source paths.
- Recorded Bundle H current gate logs under `Reports/BundleH/CurrentGate/`; compiler build, server build, the seven focused Bundle H runtime audits, and the gang-house service-location audit pass locally.
- Removed the stale Bundle D corrective/final audit limitation that claimed decoded room environment fields did not reach generated Classic records.
- Added `--audit-bundle-h-gang-houses` to verify native/default WCC gang-house source files, ten MudInfo gang-house maps, ten generated deed items, 22 generated gang-house/deed shop records, and the runtime deed/upkeep protection service.
- Promoted a WCC shop API reference-backed gang-house service-location catalog into `Librarium/Objects/gang_houses/gang_house_records.json` and generated `Data/Classic/gang_houses.json`.
- Added runtime catalog loading through `RuntimeClassicGangHouseCatalog`, marked `gang_houses` as a required Classic generated family, and wired the generated catalog into `RuntimeClassicGangHouseProtectionService`.
- Verified `--audit-bundle-h-gang-houses` PASS with `RuntimeGameplayReady=true` for 10 Classic houses, 20 service locations, 10 vault rooms, and 10 supply-shop rooms; all promoted service locations resolve to runtime rooms and current/expired/non-gang-room cleanup protection checks pass.
- Recorded the remaining gang-house limits: `Data/Classic/environment_rooms.json` still has 0 native gang-house flags, ordinary interior house ownership coverage and live deed acquisition/upkeep mutation remain evidence-blocked, MudInfo maps remain reference-only, and the listed `LibrariumLocal` handoff graph/index files are absent from the repo.
- Connected live combat targeting to shared `RuntimeLivingWorldService` NPC instances instead of static spawn definitions.
- Added live NPC instance identity to active combat encounters.
- Made combat rounds refresh target HP from living-world state and apply NPC damage back to living-world state.
- Made spell/ability NPC targeting resolve visible live NPC instances and synchronize NPC damage/healing with living-world state.
- Added the combat movement guard to dispatcher movement handling so connected sessions cannot walk away from active combat.
- Added CodeUpdate0139 Bundle 3 Classic runtime audit tooling:
  - `--audit-classic-data`
  - `--audit-classic-runtime`
  - `--audit-room "<room>"`
  - `--audit-spawns`
  - Host command: `audit room <room>`
- Added startup runtime audit output for `--run-local` and `--run-network`.
- Recorded audit evidence showing generated room descriptions are missing before runtime/Telnet.
- Recorded audit evidence showing live NPC/monster instantiation is active globally.
- Added compiler-side native map DAT room-text recovery diagnostics through `audit-room-text` and `export-room-text`.
- Promoted 2,417 binary-backed room descriptions into `Librarium/Objects/room_descriptions/native_map_room_description_records.json`.
- Added OmniVerse map-qualified room-description recovery for 800 canonical rooms not already covered by native binary evidence.
- Added exhaustive WCCMP002 default-install binary room-description recovery for 23,309 additional canonical rooms, including the user-approved repeated-room rule that same-title duplicate rooms share one description.
- Added WCCMP002 placeholder room-description recovery for the final 140 placeholder-named rooms. WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this pass, and WBBMP002 corroborates all 140 recovered titles and first description lines.
- Updated compiler room-description loading to merge all Librarium room-description files while keeping native/binary records ahead of reference gap fills.
- Added a read-only WCC Librarium exhaustion audit at `Reports/DataDecode/wcc_librarium_exhaustion_audit.md`.
- Reconciled MMExplorer SQL/MDB and OmniVerse public codex data into Librarium as reference-backed gap fills.
- Added `public_reference` and `mmexplorer_sql_reference` blocks to matched item, spell, NPC, and shop records.
- Added unmatched MMExplorer supplement files for item, spell, and NPC rows that do not map to active canonical records.
- Added OmniVerse-backed ability records under `Librarium/Objects/abilities/ability_records.json`.
- Verified required room-description probes for `room.map1.2141`, `room.map1.2140`, `room.map1.488`, and `room.map1.348`; regenerated live `Data/Classic` now audits with 26,668 rooms with descriptions and 0 rooms without descriptions.
- Added centralized command abbreviation resolution with exact aliases, unique-prefix matching, and ambiguity rejection for unsafe prefixes.
- Repaired live spawn materialization so grouped spawn candidates no longer explode into every room occupant; live NPC instances now require exact map-qualified spawn room resolution.
- Removed legacy room-number fallback from spawn placement and added hard placement validation for unresolved spawns.
- Expanded `audit room` and `audit spawns` output to prove each displayed entity's definition ID, spawn ID, source room, resolved runtime room, live `RoomId`, alignment, disposition, and combat hostility state.
- Updated room rendering to classify visible entities by runtime disposition instead of treating every high-HP NPC as hostile.
- Added a living-world regression check for `room.map1.21` proving the Guild/River room does not leak storm giants, woodelf entries, bosses, or unrelated group candidates.
- Current generated evidence for `room.map1.21` resolves to `guardsman` (`npc.14`) with neutral disposition; the user-expected `elite guardsman` requires a separate evidence-backed Librarium/data correction if the generated room source is wrong.
- Added cumulative MMExplorer room source, room interaction, exit semantic, edge state, latch binding, room reset/dependency, and trigger/script/quest evidence under `Librarium/`, `Data/Classic/`, `Evidence/`, `Reports/DataDecode/`, and `Tools/`.
- Resolved the remaining 7 distinct native/passive room-effect linkage gaps across 12 exit rows as evidence-bounded native/passive effects without inventing player commands.
- Added MegaMUD `/mp` route replay to the room native effect resolver; replay found 0 command hits for the affected map-qualified exits, so all repaired rows remain `resolved_without_player_command`.
- Added `Librarium/Objects/room_messages/room_message_records_catalog.json` and generated `Data/Classic/room_messages.json` with 117 unique native WCCMSG offset-backed room message records.
- Decoded room message categories for room action aliases/success text, doors, hidden exits, locks/keys, searches, and generic failures.
- Added room native effect/message decode reports at `Reports/DataDecode/room_native_effect_linkage_and_message_decode.md` and `.json`.
- Updated the Librarium compiler catalog reader to recognize plain `*_records.json` catalog files and avoid treating source-only `room_descriptions` files as a compiled family.
- Loaded `MajorMUD_Reforged_Cumulative_Update_0206.zip` into Librarium as current quest dialogue dispatch source, without copying package cache files or treating package `/Data` as canonical authority.
- Added 183 script records to `Librarium/Objects/scripts/mmexplorer_textblock_stage_records.json`, including 182 decoded MMExplorer textblock stage scripts.
- Added merged trigger source `Librarium/Objects/triggers/mmexplorer_room_interaction_records.json` with 284 room/exit interactions plus 83 NPC quest-entry triggers.
- Added consolidated runtime quest dialogue execution policy at `Librarium/Policies/runtime_quest_dialogue_execution_policy.json`.
- Connected runtime `talk <npc>` to generated NPC quest-entry triggers and decoded stage scripts through room-scoped live NPC lookup.
- Added atomic decoded quest clause execution for conditions, text/message branches, quest flags, item give/take, experience, alignment, and host world intents for cast/summon/teleport/remoteaction.
- Bound generated random textblock tables into runtime quest dialogue execution using the decoded independent 1..100 threshold semantics.
- Added generated world-intent target validation for quest `cast`, `summon`, `teleport`, and `remoteaction` operations.
- Added host-side quest intent commits for teleporting the active character/session and materializing summoned generated NPCs into the living world.
- Added host-side quest remoteaction event recording in the living-world journal, preserving exact decoded metadata while marking unknown native mutation as evidence-gated.
- Added durable living-world event recording for committed room/object interactions.
- Imported the user-provided final native closure/WCC decode archive as repo evidence and Librarium/reference material.
- Added new native decode evidence, reports, tools, manifests, and Librarium evidence/reference files from the archive.
- Added `Native*` server registry source files from the archive; they compile with the current server runtime.
- Merged the 241 user-confirmed final native closure server implementation slices into active runtime code and consolidated duplicate contracts instead of retaining parallel reference copies.
- Recorded archive intake details at `Reports/PackageIntake/final_native_closure_update0022_intake.json`.
- Activated verified/native and strongly-correlated Librarium sidecar data in executable generated output where evidence supports it.
- Updated the Librarium compiler to merge active `room_interactions` sidecars and to record multi-source family provenance in generated manifests.
- Added compiler cross-family validation that blocks unresolved exit targets instead of allowing stale `active_runtime` traversal.
- Added compiler smoke assertions for 367 generated triggers, strongly-correlated room-interaction sidecars, and blocked unresolved exits.
- Replaced the read-only Librarium dashboard with a navigable Classic data browser in the Reforged app. The Librarium tab now has Weapons, Armor, Classes, Races, Items, Monsters, and Rooms tabs, selected-tab search, name-only record lists, and curated game-facing stat details without evidence/source metadata.
- Changed parchment tab/list/grid text to dark readable text so the Librarium tab is legible.
- Reworked the Client play surface toward the MegaMUD-style flow: the Play tab now selects server/session profiles, `Open Terminal` launches a dedicated black terminal window, and client settings live in a separate tabbed options window behind menu/actions instead of dominating the main play screen.
- Added required Male/Female selection during Telnet character creation and persisted the selected sex on `RuntimeCharacterRecord` while keeping existing saves readable.
- Fixed the compiler room-description source matcher so plain `*_records.json` Librarium room-description catalogs, including the WCCMP002 1.11p corpus, are included in generated `Data/Classic`.
- Regenerated Classic data and verified compiler/runtime audits now report 26,668 rooms with descriptions and 0 rooms without descriptions.
- Fixed generated numeric-string handling in runtime quest dialogue arguments and item catalog numeric fields, removing the server smoke crash that reported a JSON `String` where a `Number` was expected.
- Added `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md` to track the remaining native decode work after the current server/runtime audit.
- Fixed `GuardianServices.cs` missing `System.IO` import so the full solution build passes.
- Hardened generated quest reset semantics so Classic completed quests cannot be reset or repeated on the same character.
- Expanded interaction/narrative smoke coverage for random table execution, cast/summon events, remoteaction preservation/event recording, room/object event recording, world-intent target validation, edge opcode counts, and exhaustive dispatch validation across all generated NPC quest-entry triggers.
- Added generated quest state command handling for `journal`, `quest`, `accept`, `abandon`, `complete`, and `quest reset`.
- Added interaction/narrative smoke coverage for 182 decoded stage scripts, 83 NPC quest-entry triggers, 182 linked stage script references, branch dispatch, item consumption/reward transition, and failure/no-mutation behavior.
- Added `server_interaction_narrative` to `Tools/validator.py`.
- Installed Decode Exhaustion Bundle F Revision 0033 into the local dirty CodeUpdate0139 repository without commit or push.
- Fixed Bundle F runtime/audit integration issues found after overlay: nested decoded-description projection, exact NPC corpse id/gold transfer, non-stackable smoke samples, presentation-only graph traversal, variable operand role projection, Witchhunter ability mapping, optional world reward activation terms, nullable NPC dialogue names, and current NPC dialogue execution counts.
- Installed Decode Exhaustion Bundle G Revision 0002 into the local dirty CodeUpdate0139 repository without commit or push.
- Closed the F10 native presentation payload gap with evidence-backed mappings for verified no-output decoded text nodes and exact WCCMSG message payloads for `message 2044` and `message 3290`; unknown message IDs still fail closed.
- Verified Bundle F through F15 locally after Bundle G: F10 native payload audit reports 0 disconnects, and F10 complete, F11 commands, F12 random tables, F13 events/missions, F14 rewards, and F15 persistence all pass.
- Preserved `Saves/`, `ImportedDatabases/`, and generated `/Data` authority boundaries.

Verification:

- `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore`
- `dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore`
- `dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-classic-data`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-classic-runtime`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- audit-room-text`
- `python Tools\Importer\audit_wcc_librarium_gaps.py --repo .`
- `python Tools\Importer\promote_reference_sources_to_librarium.py --repo .`
- `python Tools\Importer\recover_reference_room_descriptions.py --repo .`
- `python Tools\Importer\recover_wccmp002_room_descriptions.py --repo .`
- `python Tools\Importer\recover_wccmp002_placeholder_room_descriptions.py`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicReferenceReconcile0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data/CompilerSmokeTest/ClassicRoomText0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicRoomDescriptionReference0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicWccmp002DuplicateRule0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release -- build --world Classic --clean --output Data\CompilerSmokeTest\ClassicRoomDescriptionComplete0139`
- `dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release -- build --world Classic --clean --output Data\Classic`
- `python Tools\Importer\resolve_room_native_effect_messages.py --repo .`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-interaction-narrative`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-live-gameplay`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-living-world`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-room "room.map1.21"`
- `dotnet run --project Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj --configuration Release`
- `dotnet build Apps/Source/Reforged.App/Reforged.App.csproj --configuration Release`
- `python Tools\validator.py`
- `python _codex_staging\Decode_Exhaustion_Bundle_F_Revision_0033\Decode_Exhaustion_Bundle_F_Revision_0033\Validation\validate_bundle_f_revision_0033.py _codex_staging\Decode_Exhaustion_Bundle_F_Revision_0033\Decode_Exhaustion_Bundle_F_Revision_0033`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-e-zero-disconnects`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f10-npc-dialogue-execution`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f10-native-payloads`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f10-complete`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f11-commands`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f12-random-tables`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f13-events-missions`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f14-rewards`
- `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-f-phase-f15-persistence`
- Single validator PASS 13/13 at `Reports/RuntimeVerification/validator_latest.md`
- Latest validator PASS includes `librarium_catalog`, `generation_readiness`, solution restore/build, compiler tests, core smoke, Classic data audit, server generated data, server room exits, server NPCs, and server live gameplay checks.
- Latest validator PASS also includes `server_interaction_narrative`.
- Latest `server_interaction_narrative` PASS includes generated random textblock execution, world-intent target validation, cast/summon/remoteaction coverage, Classic non-repeat quest guard coverage, and exhaustive NPC quest-entry dispatch validation.
- Latest living-world smoke PASS includes quest remoteaction event recording and generated room/object interaction event recording.
- Final native closure archive intake verification: server Release build PASS, test Release build PASS, Classic compiler/runtime audits PASS with 26,668/26,668 room descriptions, interaction/narrative smoke PASS with 83 executable quest entries, and `python Tools\validator.py` PASS 13/13 after import and runtime audit repair.

Known limitation:

- Room-description coverage is 26,668/26,668 after WCCMP002 placeholder recovery and compiler regeneration.
- WBBMP002 is retained as secondary comparison evidence only and does not override WCCMP002 room/map source records.
- Spawn placement validation now skips and reports unresolved map-qualified spawns instead of attaching them to fallback rooms. Current unresolved placement records are `spawn.288.43` (`room.map1.288`) and `spawn.2306.213` (`room.map1.2306`).
- The 7 repaired room native/passive effects are closed as linkage classifications, not as discovered player commands. MegaMUD `/mp` evidence did not prove command bindings for those exact exits.
- `remoteaction` is recorded as a durable host event with exact decoded metadata; its native remote mutation is not guessed without additional evidence.
- Native closure implementation slices are now active runtime code after merge/replace cleanup; remaining limitations are compile warnings and GitHub Actions pending after push.

GitHub Actions is pending until CodeUpdate0139 is pushed and confirmed green.

## CodeUpdate0138 - Server Runtime and Host Enablement

- Closed the local CodeUpdate0138 baseline.
- Integrated server runtime and host enablement surfaces.
- Added/updated host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit coverage.
- Integrated live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world-system smoke surfaces.
- Promoted map-qualified MMExplorer movement records through Librarium and compiler-generated `/Data`.
- Applied MMExplorer textblock quest-flow fix 0142.
- Updated Librarium quests to 2,183 total quest records, including 83 `npc_textblock_quest_flow` records.
- Added the 0142 textblock quest validator to the complete runtime verification gate.
- Consolidated active handoff docs to the compact root/docs surface.
- Removed copied update package folders, generated Python caches, compiler smoke output, and smoke-save artifacts from the working tree.

Verification:

- `python Tools\validator.py`
- PASS 68/68

GitHub Actions is pending until pushed and confirmed green.

One-time validation proofs are recorded in ValidationManifest.json.

## GuardianHallPhase2-0149 - Dedicated Guardian Workspaces

- Replaced the shared Guardian assignment panel with five role-specific fantasy command-center workspaces.
- Made Realm Keeper global across connected hosts and world categories with no world or character assignment.
- Retained Wanderer server/world/realm/character assignment and player-like mission policies.
- Removed character control from Analyst and Worldsmith and added analysis/report and proposal/simulation workflows.
- Converted Archivist into a Librarium evidence workbench with multi-file/folder/drag-drop intake, decode queue, confidence and relationship records, preview-only knowledge output, Markdown/HTML/JSON/PDF reports, Codex package export, and an editable local-tool registry.
- Replaced generic assignment persistence with versionable role-specific Guardian Hall state persistence.
- Preserved Forge/Borin as a separate software-engineering surface.

## GuardianHallPhase3-0150 - Realm Keeper Runtime Telemetry

- Added an evidence-based local telemetry service for the global Realm Keeper workspace.
- Added initial, manual, and 30-second periodic telemetry refresh.
- Wired generated world/record counts, save activity, validator state, build reports, runtime logs, and crash/error evidence into Realm Keeper health and activity panels.
- Added observable telemetry metrics and a bounded persisted realm timeline.
- Kept LAN and Internet host scopes configuration-only until authenticated host telemetry endpoints exist.
- Preserved read-only client behavior and made no Librarium or server-state mutations.

## Guardian Hall Phase 3-0151
- Added Wanderer local character/session telemetry over repository Saves and generated room records.
- Added session refresh, evidence-backed route/inventory/progress summaries, and dynamic mission queue updates.
- Preserved Wanderer character assignment and avoided false live-session claims.

## Guardian Hall Phase 3-0152 — Analyst Runtime Data Wiring

- Added a character-free Analyst telemetry service that scans generated runtime data by selected analysis target.
- Added target mappings for combat, economy, drops, NPC balance, quest balance, experience curve, item balance, and progression.
- Added measured source-file, source-record, and existing-report counts.
- Added read-only graph readiness, historical baseline, data provenance, refresh status, and refresh time.
- Added manual Analyst data refresh and initial refresh when Guardian Hall loads.
- Expanded Analyst Markdown exports with measured counts and runtime status.
- Preserved Librarium authority and prohibited Analyst runtime or character mutation.

## Guardian Hall Phase 3-0153
- Wired Worldsmith to generated-data baselines by content family.
- Added safe test-realm inspection, persisted proposal previews, and non-mutating simulation reports.
- Blocked live-realm proposal and simulation targets; no runtime apply path exists.

## Guardian Hall Phase 3-0154 — Archivist Evidence Decode Pipeline

- Added individual and queue-wide Archivist decode commands.
- Added SHA-256 evidence identity, file-size capture, bounded inspection, and persisted findings.
- Added structured JSON, XML, CSV, text, WCC/WBB, DLL, MDB, and BIN inspection paths.
- Added preview-only evidence artifacts, queue relationships, Librarium preview summaries, and saved evidence snapshots.
- Preserved Librarium authority: no decode or save action overwrites Librarium content.

## Guardian Hall Phase 3-0155 — Archivist Reports and Local Tools

- Expanded Archivist reports into complete Markdown, JSON, HTML, and PDF investigation outputs with decoded findings, evidence, confidence, relationships, suggested-runtime cautions, and open questions.
- Expanded Codex exports into a multi-file handoff package with investigation summary, decoded structures, evidence, relationships, suggested runtime, open questions, and a package manifest.
- Added persisted editable local-tool working directories, enable/disable state, validation status, last launch time, and launch errors.
- Added executable and working-directory validation before launching local tools.
- Added `{file}`, `{folder}`, and `{root}` argument tokens without hardcoded paths.
- Preserved preview-only Librarium behavior and introduced no runtime promotion or silent overwrite path.

## Guardian Hall Phase 3-0156 — Worldsmith Approval-Controlled Staging

- Added a generated approval code and separate entered-code confirmation gate.
- Bound approvals to the exact proposal SHA-256 and safe target realm.
- Added persisted approval artifacts, isolated test-realm staging, apply receipts, and rollback backups.
- Blocked Live Realm, disabled approval bypass, and rejected proposals that differ from the approved source.
- Declared and validated that staging does not mutate runtime data or Librarium content.

## GuardianHallPhase3-0157 — Phase 1 Persistence Migration
- Added schema-versioned Guardian Hall state.
- Added one-time migration from legacy `GuardianHall/assignments.json`.
- Preserved the legacy file as `.phase1-backup` and wrote a migration receipt.
- Mapped generic Phase 1 fields into role-specific workspaces without introducing character control for Analyst, Worldsmith, or Archivist.
- Preserved Wanderer character assignment and protected Worldsmith from migrated Live Realm targets.

## Guardian Hall Phase 3-0158
- Added persisted Guardian scheduling, job history, restart recovery, cancellation, pause/resume, and role-safe execution.
- Added Guardian Activity Center UI.
- Kept Worldsmith apply operations manual and approval-gated.

## Guardian Hall Phase 3-0159
- Added persisted configurable LAN and Internet Realm Keeper telemetry endpoints.
- Added bounded authenticated HTTP telemetry using bearer tokens read only from named environment variables.
- Added endpoint status, last-contact, and error tracking with unavailable hosts excluded from connected totals.
- Merged verified remote world, population, event, and health data with local repository telemetry.

## Guardian Hall Phase 3-0160
- Added authenticated live Wanderer session telemetry and guarded one-step mission dispatch.
- Added environment-only bearer authentication, bounded timeout, endpoint configuration, and local-evidence fallback.
- Advanced Guardian Hall persistence schema to version 5.

## Guardian Hall Phase 3-0161
- Added persisted Analyst historical snapshots and prior-baseline comparisons.
- Added rendered family distribution series and Markdown/JSON/CSV export packages.
- Added validated configurable Analyst output locations.

## Guardian Hall Phase 3-0162
- Added indexed Archivist evidence search across decoded names, paths, hashes, findings, confidence, and relationships.
- Added selectable evidence results with direct and reverse cross-reference inspection.
- Added bounded multi-term search and clear-search controls.
- Preserved preview-only Librarium behavior and read-only evidence browsing.

## Guardian Hall Phase 3-0163
- Added distinct visual identity styles and purpose badges for all five Guardian chambers.
- Preserved all role-specific controls and kept Forge/Borin separate.

## CodeUpdate0139 — MegaMUD Classic Terminal Presentation
- Rebuilt the dedicated Client terminal window around the verified MegaMUD workflow: File/Edit/View/Action/Options/Help menu bar, compact one-click auto-option toolbar, conversation selector, black primary terminal, right-side map/items/character tools, classic command line, and blue status bar.
- Preserved all existing Reforged session, automation, route, parsing, coordination, map, and Librarium services; the change is Client presentation and command wiring only.
- Added command-history navigation, quick gameplay commands, reconnect, route controls, optional modern side panels, and channel synchronization between the conversation list and output tabs.
