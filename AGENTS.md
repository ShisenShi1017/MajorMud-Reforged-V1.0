# Reforged Agent Instructions

These instructions are mandatory for every Codex, ChatGPT, AI assistant, and developer handoff working on this repository.

## Current Position

- Mode: Codex local repository work.
- Repository: `ShisenShi1017/MajorMud-Reforged-V1.0`.
- Local path: `E:\MajorMud-Reforged Github`.
- Current Code Update: `0139`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0139 - Telnet Runtime Cohesion`.
- Current status: local runtime cohesion changes applied; awaiting user push and green GitHub Actions confirmation.
- CodeUpdate0138 status: green by user report.
- Latest local verification: Bundle H final audit sweep PASS 63/63; final smoke sweep PASS across all remaining `--smoke-*` commands after repaired item, living-world, command-alias, and player-combat-profile gates; solution Release build PASS with warnings-as-errors; compiler tests PASS; core/server smoke tests PASS; server and app win-x64 publish PASS; generated Classic data regenerated from Librarium before server bootstrap. Python validator could not be run in this Codex environment because `python`/`py` are not accessible to the process.
- GitHub Actions corrective note: `/Data/Classic` is not committed. The workflow must run the Librarium compiler before server bootstrap diagnostics.

## Active Documentation

Use the small active doc surface only:

- `AGENTS.md`
- `AI_CONTEXT.md`
- `README.md`
- `CHANGELOG.md`
- `VERSION.md`
- `ReleaseNotes.md`
- `ProjectManifest.json`
- `UpdateManifest.json`
- `MANIFEST.md`
- `HANDOFF.md`
- `PATCH_INSTRUCTIONS.md`
- `LibrariumManifest.json`
- `Docs/PROJECT_STATUS.md`
- `Docs/Architecture/PROJECT_ROADMAP.md`
- `Docs/Architecture/HOST_VISION_V1.md`
- `DEVELOPMENT_PRINCIPLES.md`

Do not recreate old per-update handoff/changelog/manifest clutter unless the user explicitly asks.

## Source of Truth

- GitHub is the authoritative pushed source of truth.
- Codex normally edits the local repository only; if the user explicitly asks Codex to push, push only after local verification and commit succeed.
- Do not claim GitHub Actions is green unless GitHub or the user confirms it for the current pushed commit.
- Local ZIPs and prior chat handoffs are input material only; they do not override the repo.

## Verification Rules

- Never guess method names, paths, update numbers, build state, package contents, or milestone state.
- Verify before claiming something exists, passed, failed, or changed.
- If verification cannot be run, say exactly what was verified and what remains unverified.
- Local build/test results are useful, but GitHub Actions is the pushed gate after the commit is pushed.

## Required Startup Check

Before code or architecture work, read or verify:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/PROJECT_STATUS.md`
4. `Docs/Architecture/PROJECT_ROADMAP.md`
5. `README.md`
6. `VERSION.md`
7. `CHANGELOG.md`
8. `ProjectManifest.json`
9. `DEVELOPMENT_PRINCIPLES.md` when architecture, runtime, or code-quality decisions are involved
10. Relevant source/test files for the requested change

## Architecture Rules

- Librarium is the only gameplay authority.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data` only.
- CI and local bootstrap validation must regenerate generated `/Data` from Librarium before running the server if the checkout is clean.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Server/Host is authoritative for world state, character state, combat, inventory, NPCs, quests, economy, timers, resets, and permissions.
- `ImportedDatabases/` must remain untouched unless the user explicitly approves a reviewed cleanup.
- Preserve real `Saves/`; do not commit smoke-save artifacts.

## CodeUpdate Rules

Every Code Update must:

- Confirm current Code Update, Database Version, and milestone from repo files.
- Make the smallest safe change for the approved scope.
- Update the active continuity docs listed above.
- Record verification status and known limitations.
- Wait for green GitHub Actions before moving to the next public update, unless the user explicitly overrides the gate.

## CodeUpdate0139 State

Verified locally in this working copy:

- Telnet sessions route through the authoritative runtime command dispatcher.
- Live combat targeting uses shared living-world NPC instances.
- Combat rounds and spell/ability NPC effects update shared living-world state.
- Movement checks the active combat guard before traversal.
- Bundle 3 Classic runtime audit tooling is present.
- Audit finding: generated `Data/Classic` now contains 26,668/26,668 room descriptions after the WCCMP002 placeholder recovery and compiler regeneration.
- Audit finding: live-world materialization currently creates 1,015 live NPC instances and 628 live monster instances after hard map-qualified placement validation.
- Compiler room-text recovery tooling is present through `audit-room-text` and `export-room-text`.
- Librarium now contains 2,417 clean native-runtime room descriptions, 800 lower-priority OmniVerse map-qualified reference gap fills, and 23,309 WCCMP002 default-install binary gap fills under `Librarium/Objects/room_descriptions/`.
- Compiler room-text loading merges all Librarium room-description files with native/binary records taking priority over reference gap fills.
- A read-only WCC Librarium exhaustion audit records the current source coverage and remaining blockers at `Reports/DataDecode/wcc_librarium_exhaustion_audit.md`.
- MMExplorer SQL/MDB and OmniVerse public codex data were reconciled into Librarium as reference-backed gap-fill data. Matched item/spell/NPC/shop records now carry `public_reference` and `mmexplorer_sql_reference`; unmatched MMExplorer rows are retained as supplement files; OmniVerse abilities are represented as a new `abilities` family.
- Live `Data/Classic` was regenerated from Librarium after the room-description promotion and now audits with 26,668 rooms with descriptions and 0 rooms without descriptions.
- WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this recovery pass. WBBMP002 is secondary comparison evidence only.
- Local WCCMP002 placeholder room-description recovery promoted the remaining 140 placeholder-named rooms, WBB-corroborated all 140 titles and first description lines, and brought room-description coverage to 26,668/26,668.
- Runtime command abbreviation handling is centralized with exact aliases, unique-prefix resolution, and ambiguity rejection.
- Live spawn placement requires exact map-qualified room resolution and no longer falls back to numeric room IDs, the current room, start room, or shared/default buckets.
- Grouped spawn records no longer materialize every candidate NPC into a room; live materialization uses explicit generated room NPC definitions where present and logs/skips unresolved placement.
- Runtime room audits include displayed entity placement proof: definition ID, spawn ID, source room, resolved runtime room, live `RoomId`, alignment, disposition, and combat hostility state.
- `room.map1.21` currently audits as one neutral `guardsman` (`npc.14`) from `spawn.21.5`; the user-expected `elite guardsman` is not supported by the current generated room source field and remains a separate evidence-backed data correction if needed.
- Current hard placement validation skips two classified invalid/non-spawn records: `spawn.288.43` points to non-existent `room.map1.288`, and `spawn.2306.213` points to `room.map1.2306`, which the user verified is a shop with no NPC.
- The remaining 7 distinct room native/passive effect linkage gaps across 12 exit rows are resolved as `native_passive_effect_resolved`, with `resolved_without_player_command` and no promoted command.
- MegaMUD `/mp` route replay was used for those exact map-qualified exits and found 0 command hits, so no player command binding was guessed.
- Native WCCMSG room action/door/hidden-exit/lock/search/failure strings are cataloged as 117 unique offset-backed room message records.
- Decode Exhaustion Bundle G Revision 0002 is installed locally and closes the prior F10 native presentation payload gap. Verified no-output decoded text nodes are explicit, `message 2044` and `message 3290` resolve through exact WCCMSG payload evidence, unknown message IDs fail closed, and local F10 native payload plus F10 complete through F15 audits pass.
- Bundle H baseline report is recorded at `Reports/BundleH/BASELINE_RUNTIME_CONSOLIDATION_AUDIT.md`. Safe repairs added `search` to the supported runtime command catalog, aligned Bundle D search audit semantics with transient reroll/replace behavior, scoped Bundle D door validation to door/object failures, adjusted Bundle A repository-coverage gating to fail on true generated-unconsumed fields while recording evidence-name-only warnings, and promoted the two imported WCC room-anchor environment records that were absent from the MMExplorer-derived environment catalog.
- Bundle A character audit now passes with 13 races, 15 classes, no generated-unconsumed Bundle A gameplay fields, and 57 repository-wide evidence-name-only warnings recorded separately from runtime disconnects.
- Bundle D final now passes locally with `AuditPassed=true` and `BundleDComplete=true`. D9 now reports 26,668 runtime rooms and 26,668 decoded environment records. `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` are backed by imported WCC room records and source-classified as `decoded_imported_wcc_room_record_anchor`. D9 explicitly asserts both anchor records and their decoded values. Neither room is a gang house, so gang-house environment rows remain 0.
- Bundle E zero-disconnect audit now passes locally with `BundleEComplete=true`, 1,100 NPC definitions, 962 spawn records, all 13 NPC/creature subsystems passed, and no duplicate authoritative creature source paths. Focused companion, merchant/trainer, boss/death/respawn, NPC item/control/persistence, and relationship/service closure audits also pass.
- Bundle H current gate logs are preserved under `Reports/BundleH/CurrentGate/`; the compiler build, server build, seven focused runtime audits, and the gang-house service-location audit pass locally.
- Bundle H final gate logs are preserved under `Reports/BundleH/FinalGate/`. Final local gate repairs corrected bank item location validation, isolated owner-limited item smoke state, restored exact `ba` -> `bash` command parsing, refreshed living-world smoke expectations to generated 26,668-room / 367-interaction data, and split player combat stored-field checks from derived backstab/dodge formula consumers.
- Bundle H gang-house service-location audit is recorded at `Reports/BundleH/CurrentGate/bundle_h_gang_house_audit.json`. Native/default WCC gang files, ten MudInfo gang-house maps, ten deed items, 22 gang-house/deed shops, and deed/upkeep cleanup protection are verified. Librarium now contains `Librarium/Objects/gang_houses/gang_house_records.json`, generated Classic data contains `Data/Classic/gang_houses.json`, and runtime consumes the generated catalog through `RuntimeClassicGangHouseCatalog` and `RuntimeClassicGangHouseProtectionService`.
- Gang-house service-location scope is 10 Classic houses, 20 WCC shop API-backed service locations, 10 vault rooms, and 10 supply-shop rooms; all promoted locations resolve to runtime rooms and current/expired/non-gang-room protection checks pass. Normal interior house ownership coverage and live deed acquisition/upkeep mutation remain blocked until exact evidence is recovered. `Data/Classic/environment_rooms.json` still has 0 native gang-house flags, MudInfo maps remain reference-only, and `LibrariumLocal/core_graph.json` plus `LibrariumLocal/runtime_index.json` are absent from the repo.
- Quest dialogue dispatch package 0206 is loaded into Librarium as current source records: 182 decoded textblock stage scripts and 83 NPC quest-entry triggers now generate into `Data/Classic`.
- Runtime `talk <npc>` resolves room-scoped live NPCs, dispatches generated NPC quest-entry triggers, evaluates decoded stage scripts atomically, persists character quest flags/items/experience/alignment, and fails closed without mutation when unsupported or unresolved behavior is encountered.
- Generated random textblock tables execute through decoded independent 1..100 threshold bindings.
- Generated quest world intents validate cast/summon/teleport/remoteaction targets against generated `/Data` before commit.
- Quest teleport intents update the active session and character room; summon intents materialize generated NPCs in the living world.
- Quest remoteaction intents are recorded into the living-world event journal with exact decoded metadata and `native_behavior=evidence_gated_not_mutated`; no unknown native remote mutation is guessed.
- Room/object interactions now publish durable living-world `world-object-interacted` events after persisted state commits.
- The user-provided final native closure/WCC decode archive has been imported as repo evidence and Librarium/reference material. New `Native*` registry source files compile with the server, and the user-confirmed runtime implementation slices are merged into active server runtime code with duplicate contracts consolidated.
- Intake report: `Reports/PackageIntake/final_native_closure_update0022_intake.json`.
- Verified/native and strongly-correlated Librarium records now flow into executable generated `/Data`: `room_interactions` merges the active base file plus strongly-correlated sidecars, generated `triggers` contains 367 strong records including 83 NPC quest-entry triggers, and compiler cross-family validation blocks unresolved exit targets instead of allowing stale active traversal.
- The Reforged app Librarium workspace is now a real read-only dashboard tab for Librarium families, generated Classic data files, promotion notes, and reports. Parchment tab/list/grid text uses dark readable text instead of white-on-light text.
- The Reforged app Client play flow is now MegaMUD-style: Play selects a server/session, actual gameplay opens in a dedicated black terminal window, and client settings/tools stay behind menus or a separate tabbed options window.
- Classic completed quests cannot be repeated on the same character. Replaying a completed quest requires starting a new character; same-character quest reset/restart must remain blocked for Classic.
- `Tools/validator.py` includes `server_interaction_narrative`; latest PASS includes quest remoteaction event recording and generated room/object interaction event recording.
- One-time room semantic and trigger/script/quest validations are accepted in ValidationManifest.json.
- `Saves/` and `ImportedDatabases/` remain preserved.

Next update begins only after CodeUpdate0139 is pushed and GitHub Actions is green.
- One-time validation proofs are recorded in ValidationManifest.json.
