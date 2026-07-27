# MajorMUD Reforged Project Status

This is the current live handoff for Codex, ChatGPT, and human project work.

## Mode

- Active mode: Codex local repository work.
- Repository: `ShisenShi1017/MajorMud-Reforged-V1.0`.
- Local path: `E:\MajorMud-Reforged Github`.
- Current user request: apply CodeUpdate0139 handoff work locally and verify; Codex is not pushing unless explicitly asked.

## Current Baseline

- Current Code Update: `0139`.
- Current Database Version: `0010`.
- Current Milestone: `CodeUpdate0139 - Telnet Runtime Cohesion`.
- Milestone 1: complete and frozen.
- CodeUpdate0137: complete and folded into the local baseline.
- CodeUpdate0138 status: green by user report.
- CodeUpdate0139 local status: runtime cohesion, room-description recovery, final native closure/WCC decode archive intake, verified/strong Librarium activation, Librarium app dashboard, Bundle H baseline/final audit capture, and server/native-runtime audit repairs applied and locally verified.
- Latest local server build: PASS for `MajorMud.Reforged.sln` Release with warnings-as-errors after Bundle H final gate repairs.
- Latest local final gate: PASS 63/63 Bundle H audits; all final smoke segments pass after item-runtime, command-alias, living-world, and player-combat-profile repairs; published server EXE passes Classic data, item runtime, live gameplay, living-world, player combat profile, and telnet listener startup on `127.0.0.1:4000`; published app EXE passes shell/settings/cleanup smoke routes and brief GUI launch/startup check after desktop logger hardening.
- GitHub Actions corrective fix: generated `Data/Classic` is no longer tracked in Git; the workflow now restores/builds the Librarium compiler and regenerates Classic runtime data before server bootstrap diagnostics.
- Clean generated-data repair: compiler output now includes the Librarium-backed `textblock_command_ability_contracts` family, generated JSON readers support chunk manifests, and the runtime derives NPC AI spawn relationships from generated `spawns.json` when optional `npc_ai_runtime.json` is absent.
- Local limitation: Python validator could not be rerun in this Codex process because `python`/`py` are not accessible.
- GitHub Actions status for CodeUpdate0139: pending until pushed and confirmed green.
- Guardian Hall engineering department is implemented locally across phases 1-10 with persisted sessions/tasks/memory/reports/statistics, isolated workspaces, guarded build/validation/runtime commands, repository analysis, diagnosis, and cumulative candidate ZIP packaging.
- Current Guardian Hall policy: autonomous engineering is off by default; live-repository editing is a separate off-by-default authority. The intended current workflow is isolated workspace work followed by reviewable cumulative ZIPs.
- Guardian Hall and the full solution pass Release compilation with warnings-as-errors after generated shop markup was connected to the existing native economy markup authority and strict nullable diagnostics were resolved.

## Architecture Rules

- Librarium is the only gameplay authority.
- `/Data` is generated runtime output.
- Clean checkouts must regenerate `/Data` from Librarium before server/runtime bootstrap validation.
- Runtime consumes generated `/Data` only.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- `ImportedDatabases/` remains untouched unless the user explicitly approves a reviewed cleanup.
- Preserve real `Saves/`; do not commit smoke-save artifacts.

## CodeUpdate0139 Local State

- Telnet sessions route through the authoritative runtime command dispatcher.
- Live room rendering uses living-world NPC instances, players, ground items, exit state, and stat line.
- Combat targeting now selects visible live NPC instances instead of static spawn definitions.
- Combat round damage now updates shared living-world NPC state.
- Spell/ability NPC damage and healing now update shared living-world NPC state and synchronize active combat encounter HP.
- Movement now checks the live combat guard before traversal.
- Classic runtime audit tooling now reports compiler output counts, runtime load counts, room inspection details, and spawn materialization.
- Latest audit finding: regenerated `Data/Classic/rooms.json` has 26,668 rooms with descriptions and 0 rooms without descriptions.
- Latest audit finding: live-world materialization creates 1,015 live NPC instances and 628 live monster instances after hard map-qualified placement validation.
- CodeUpdate0139 room-text recovery work now adds a compiler-side native map text resolver, `audit-room-text`, and `export-room-text`.
- Librarium now contains 2,417 binary-backed room-description records promoted from clean available native map DAT evidence and proved against required map 1 probes such as `room.map1.2141`.
- Librarium also contains 800 lower-priority OmniVerse map-qualified reference gap-fill room descriptions for canonical rooms not already covered by native evidence.
- Librarium now contains 23,309 WCCMP002 default-install binary gap-fill room descriptions recovered from `ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat`.
- Librarium now contains 140 additional WCCMP002 placeholder room descriptions recovered from decoded native offsets; WBBMP002 corroborates all 140 titles and first description lines.
- Compiler room-text projection merges all room-description files with native/binary evidence taking priority; current compiler audit resolves 26,668/26,668 room descriptions.
- Room-description reference recovery report: `Reports/DataDecode/room_description_reference_recovery.md`.
- WCCMP002 room-description recovery report: `Reports/DataDecode/wccmp002_room_description_recovery.md`.
- WCCMP002 placeholder room-description recovery report: `Reports/DataDecode/wccmp002_placeholder_room_description_recovery.md`.
- Read-only WCC exhaustion reporting now records current source coverage at `Reports/DataDecode/wcc_librarium_exhaustion_audit.md`.
- MMExplorer SQL/MDB and OmniVerse public codex reconciliation now records structured reference-backed gap fills at `Reports/DataDecode/mmexplorer_omniverse_librarium_reconciliation.md`.
- Matched item, spell, NPC, and shop records now include MMExplorer and OmniVerse reference blocks; unmatched MMExplorer records are retained as supplements; OmniVerse abilities are now represented as a new Librarium family.
- Live `Data/Classic` was regenerated from Librarium after the room-description promotion and now audits with complete room-description coverage.
- WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this recovery pass. WBBMP002 remains secondary comparison evidence only.
- CodeUpdate0139 command abbreviation work now uses a centralized command registry with exact aliases, unique-prefix resolution, and ambiguity rejection for unsafe prefixes such as `s`, `se`, and `st`.
- Live spawn placement now requires exact map-qualified room resolution. Legacy numeric room fallback was removed so unresolved spawns are logged and skipped instead of materializing in arbitrary rooms.
- Live room rendering now consumes only room-scoped living NPC instances and colors by runtime disposition/alignment instead of treating every high-HP NPC as hostile.
- Focused placement audit for `room.map1.21` proves one visible neutral NPC from `spawn.21.5`, with `sourceRoom=room.map1.21`, `resolvedRoom=room.map1.21`, and `liveRoom=room.map1.21`; no storm giants, woodelf entries, bosses, or unrelated group candidates are visible there.
- Current generated evidence for `room.map1.21` resolves the occupant as `guardsman` (`npc.14`) from `(Max 1): 14,[5-1-1-1]`. The user-expected `elite guardsman` remains a separate evidence-backed Librarium/data correction if the source record is wrong.
- Current hard placement validation skips two classified invalid/non-spawn records: `spawn.288.43` points to non-existent `room.map1.288`, and `spawn.2306.213` points to `room.map1.2306`, which the user verified is a shop with no NPC.
- Current generated Classic data after the folded material: 26,668 rooms, 61,976 exits, 291 room interactions, 367 triggers, 183 scripts, and 2,183 quests.
- The remaining room native/passive effect linkage gaps are closed: 7 distinct native/passive effects across 12 exit rows now compile as `native_passive_effect_resolved` with no promoted player command.
- MegaMUD `/mp` route replay was used for path evidence against the affected map-qualified exits and found 0 command hits, so no command binding was guessed.
- Native WCCMSG room-action, door, hidden-exit, lock/key, search, and failure strings are cataloged as 117 unique offset-backed message records in Librarium and generated `/Data`.
- Room native effect/message report: `Reports/DataDecode/room_native_effect_linkage_and_message_decode.md`.
- Quest dialogue dispatch intake from `MajorMUD_Reforged_Cumulative_Update_0206.zip` is loaded into Librarium as current script/trigger source records.
- Generated `Data/Classic/scripts.json` now contains 183 script records, including 182 decoded textblock stage scripts.
- Generated `Data/Classic/triggers.json` now contains 367 trigger records, including 83 NPC quest-entry triggers and 182 linked stage script references.
- Runtime NPC dialogue dispatch is connected to `talk <npc>` through room-scoped live NPC lookup, generated NPC quest-entry triggers, decoded stage-order script evaluation, atomic local mutation, and fail-closed rollback.
- Runtime quest state commands expose generated `journal`, `quest`, `accept`, `abandon`, `complete`, and guarded `quest reset` state markers. Classic completed quests cannot be repeated on the same character; replay requires starting a new character.
- Generated random textblock tables execute through decoded independent 1..100 threshold semantics.
- Generated quest world-intent targets are validated for cast, summon, teleport, and remoteaction before commit.
- Quest teleport intents update the active character/session room; summon intents materialize generated NPCs into the living world.
- Quest remoteaction intents are recorded as durable living-world events with exact decoded metadata and an evidence-gated native-behavior marker; the host does not guess the unknown native remote mutation.
- Room/object interaction commits now emit durable `world-object-interacted` living-world events.
- Final native closure/WCC decode archive intake is applied: 1,673 new files copied into the repo, native decode/Librarium evidence added, `Native*` registry source files compile, and 241 user-confirmed server implementation slices were merged into active runtime code with duplicate contracts consolidated instead of leaving parallel copies.
- Existing differing files skipped during archive intake are listed in `Reports/PackageIntake/final_native_closure_update0022_intake.json`.
- Verified/native and strongly-correlated Librarium records now flow into executable generated `/Data` where evidence supports activation. `room_interactions` compiles from the active base records plus strongly-correlated sidecars, generated `triggers` contains 367 strong records including 83 NPC quest-entry triggers, and compiler cross-family validation blocks unresolved exit targets.
- The Reforged app Librarium workspace is now a readable read-only Classic data browser with Weapons, Armor, Spells, Abilities, Classes, Races, Items, Monsters, Quests, and Rooms tabs, selected-tab search, name-only left-side record lists, character-foundation class/race gameplay details, and nontechnical quest guidance showing who to talk to, what to say, needed items, and plain-language steps.
- The Reforged app Client workspace is now oriented around the MegaMUD-style play flow: choose a server/session in Play, open a dedicated black terminal window, and keep client settings/tools in menus or a separate tabbed options window.
- Telnet character creation now prompts for Male/Female before final character build, saves the selected sex on the character record, and keeps older saves backward-compatible as `Unspecified`.
- Compiler room-description loading now recognizes both `*_records.json` and `*_records_*.json` source files, so the WCCMP002 room-description corpus projects into generated `Data/Classic`.
- Runtime quest dialogue and item catalog readers now accept generated integer-compatible numeric strings for quest arguments and item economy/field values, fixing the local JSON `String` versus `Number` server smoke crash.
- Current Classic runtime audit counts: 26,668 rooms loaded, 26,668 room descriptions loaded, 1,100 NPC definitions, 1,078 monster definitions, 962 spawn records, 1,015 live NPC instances, 628 live monster instances, 1,648 doors, 178 shops, 27 banks, 44 trainers, 2,183 quests, and 367 triggers.
- Bundle H final verification counts: generated/runtime room coverage is now 26,668 rooms with 26,668 descriptions; living-world smoke reports 962 generated spawns, 61,976 exits, 367 generated interactions, 1,015 runtime NPC instances, 1,932 world objects, 17 classified gate kinds, and the classified nonblocking `npc-ai-catalog-invalid` signal.
- Decode Exhaustion Bundle G Revision 0002 is installed locally and closes the prior Bundle F native presentation payload gap. `message 2044` and `message 3290` resolve through exact WCCMSG evidence, verified no-output decoded text nodes are mapped explicitly, unknown message IDs fail closed, and local F10 native payload plus F10 complete through F15 audits pass.
- Bundle H baseline is recorded at `Reports/BundleH/BASELINE_RUNTIME_CONSOLIDATION_AUDIT.md`. Current gate logs are preserved under `Reports/BundleH/CurrentGate/`. Server and compiler builds pass with warnings-as-errors. Bundle A character, Bundle D search, door, corrective, environment, and final audits now pass after adding `search` to the supported command catalog, aligning search audit semantics with transient reroll/replace behavior, scoping door validation to door/object failures, separating Bundle A evidence-name-only warnings from true generated-unconsumed disconnects, and promoting the two imported WCC room-anchor environment records that were absent from the MMExplorer-derived environment catalog.
- Bundle D final now passes locally with `AuditPassed=true` and `BundleDComplete=true`. D9 now reports 26,668 runtime rooms and 26,668 decoded environment records. `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` are backed by imported WCC room records and source-classified as `decoded_imported_wcc_room_record_anchor`. D9 explicitly asserts both anchor records and their decoded values. Neither room is a gang house, so gang-house environment rows remain 0.
- Bundle E zero-disconnect audit now passes locally with `BundleEComplete=true`, 1,100 NPC definitions, 962 spawn records, all 13 NPC/creature subsystems passed, and no duplicate authoritative creature source paths. Focused companion, merchant/trainer, boss/death/respawn, NPC item/control/persistence, and relationship/service closure audits also pass.
- Bundle H gang-house service-location audit is recorded at `Reports/BundleH/CurrentGate/bundle_h_gang_house_audit.json`. Native/default WCC gang files, ten MudInfo gang-house maps, ten deed items, 22 gang-house/deed shops, and deed/upkeep cleanup protection are verified. Librarium now contains `Librarium/Objects/gang_houses/gang_house_records.json`, generated Classic data contains `Data/Classic/gang_houses.json`, and runtime consumes the generated catalog through `RuntimeClassicGangHouseCatalog` plus `RuntimeClassicGangHouseProtectionService`.
- Gang-house service-location scope is 10 Classic houses, 20 WCC shop API-backed service locations, 10 vault rooms, and 10 supply-shop rooms; all promoted locations resolve to runtime rooms and current/expired/non-gang-room protection checks pass. Normal interior house ownership coverage and live deed acquisition/upkeep mutation remain blocked until exact evidence is recovered. `Data/Classic/environment_rooms.json` still has 0 native gang-house flags, MudInfo maps remain reference-only, and `LibrariumLocal/core_graph.json` plus `LibrariumLocal/runtime_index.json` are absent from the repo.
- Bundle H baseline blockers from the first sweep are cleared locally. GitHub Actions remains pending until CodeUpdate0139 is pushed and confirmed green.
- Remaining native decode gaps are recorded at `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md`.
- Native runtime evidence policy for this milestone: proven and strongly-correlated native behavior should be wired into executable Classic runtime; exact native behavior that remains unproven, such as unresolved remote mutation semantics, stays evidence-gated and must not be guessed.
- Classic completed quests cannot be reset or restarted on the same character.
- One-time room semantic and trigger/script/quest validations are accepted and recorded in `ValidationManifest.json`.
- Latest audit report: `Reports/RuntimeVerification/classic_runtime_audit.md`.
- `Saves/` and `ImportedDatabases/` remain preserved.

## Verification

Latest local verification:

```text
python Tools\validator.py
```

Result:

```text
PASS 13/13
All current-health validator checks passed.
Living-world smoke PASS with hard placement audit signal.
Focused room audit PASS for room.map1.21: one neutral room-scoped visible NPC and no group-5 leakage.
Classic audit report written at Reports/RuntimeVerification/classic_runtime_audit.md.
Librarium room-text promotion contains 2,417 native-backed descriptions, 800 reference-backed descriptions, 23,309 WCCMP002 binary-backed gap fills, and 140 WCCMP002 placeholder room recoveries; compiler smoke and live Data/Classic output emit 26,668/26,668 descriptions.
Room native/passive effect closure PASS: 12/12 target exits resolved, 0 old unbound statuses remain, and 117 unique room message records compile.
Quest dialogue dispatch PASS: 182 decoded stage scripts, 83 NPC quest-entry triggers, 182 linked stage script references, branch dispatch, item consumption/reward transition, and failure/no-mutation behavior.
Quest/script edge closure PASS: random table execution, cast/summon/remoteaction coverage, world-intent target validation, Classic non-repeat quest guard, and exhaustive NPC quest-entry dispatch validation.
Remoteaction and room/object event recording PASS.
Final native closure archive intake PASS; server/test builds and single validator PASS after active merge of implementation slices, duplicate contract cleanup, room-description catalog resolver repair, and generated numeric-string runtime reader repairs.
Single validator PASS.
Compiler smoke PASS: 99,366 records across 37 families.
Reforged app Release build PASS; app publish PASS to `Release/MajorMudReforged/MajorMudReforged.exe`.
Reforged app startup smoke PASS after the MegaMUD-style terminal-window UX repair.
Server Release build PASS after Telnet character sex-selection repair.
Bundle H server build PASS with warnings-as-errors.
Bundle D search audit PASS.
Bundle D door audit PASS.
Bundle D corrective audit PASS.
Bundle A character audit PASS.
Bundle D environment audit PASS with 26,668/26,668 decoded environment rows.
Bundle D final audit PASS with `AuditPassed=true` and `BundleDComplete=true`.
Bundle E companion lifecycle audit PASS.
Bundle E merchant/trainer closure audit PASS.
Bundle E boss/death/respawn closure audit PASS.
Bundle E NPC item/control/persistence closure audit PASS.
Bundle E relationship/service closure audit PASS.
Bundle H current gate PASS for the focused runtime audits with logs under `Reports/BundleH/CurrentGate/`.
Bundle H gang-house service-location audit PASS with `RuntimeGameplayReady=true` for the generated WCC shop API service-location catalog; full live deed acquisition/upkeep mutation and ordinary interior house ownership coverage remain evidence-blocked.
Bundle E zero-disconnect audit PASS with `BundleEComplete=true`.
```

GitHub Actions is not claimed green until the pushed commit is verified.

## Next Step

Push CodeUpdate0139 when approved and wait for green GitHub Actions before beginning the next public update.

## Documentation Cleanup Policy

The repository uses a compact active doc surface. Do not recreate old per-update handoff/changelog/manifest trees unless the user explicitly asks.

Current live continuity should be kept here and mirrored briefly in:

- `AGENTS.md`
- `AI_CONTEXT.md`
- `README.md`
- `VERSION.md`
- `CHANGELOG.md`
- `ReleaseNotes.md`
- `ProjectManifest.json`
- `UpdateManifest.json`
- `Docs/Architecture/PROJECT_ROADMAP.md`

Historical detail should stay in structured evidence/report data when it is still needed, not in duplicate root or Docs handoff chains.
- One-time validation proofs are recorded in ValidationManifest.json.

## Client Terminal Presentation Update
- The dedicated gameplay terminal now uses the MegaMUD Classic layout as its default presentation: six-menu command bar, compact auto-option toolbar, Conversations list, black terminal, command entry, blue status strip, and optional Reforged map/items/character side tools.
- The modern additions are layered around the terminal and can be hidden; they do not replace the text-first MegaMUD workflow or move gameplay ownership into the Client.
- Static XAML and event-handler validation passed in the delivery environment. A fresh Windows .NET build remains required after installation.
