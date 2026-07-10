# Reforged Agent Instructions


## Current Baseline - CodeUpdate 0135

- Librarium is the authoritative source of truth.
- `/Data` is generated runtime output per world.
- `/ImportedDatabases` must be retained until the Librarium is 100% complete and the user explicitly approves retirement.
- Host/server/editor modifies Librarium world definitions and overlays.
- Client is read-only and receives data for the active connected world.
- CodeUpdate 0135 is the verified local baseline after final exhaustive Classic fixes, DLL-verified single-swing combat smoke, Classic bug-fix contracts, and complete runtime promotion gate.
- CodeUpdate 0136 is the active internal milestone for complete Classic single-player gameplay. Do not create partial public CodeUpdate0136 packages or commits; work in internal phases until the consolidated 0136 objective passes verification.
- Current internal 0136 phase repairs Classic kill-time drop percent rolls. Librarium records full-width integer `1..100` semantics, Runtime uses `roll <= drop_percent`, and local complete runtime verification passed 50/50.
- CodeUpdate 0127A repairs the CodeUpdate-0127 clean-checkout bootstrap failure by tracking `Bin/.gitkeep` while generated `Bin/` contents remain ignored.
- Keep docs, handoffs, evidence, and decision records aligned with this model.


These instructions are mandatory for every AI assistant, Codex session, ChatGPT session, and developer handoff working on this repository.

## 1. Identify the work mode first

Before changing anything, identify which environment is being used.

### Codex mode

Use Codex mode only when the assistant is operating inside a Codex/local-repository coding environment.

Rules:

- Codex updates the local repository only.
- Codex does not create ZIP-based local handoff packages unless the user explicitly asks.
- Codex must wait after each update until the user pushes to GitHub and states that the current push has a green GitHub Actions result.
- Codex must not continue to the next Code Update until the current pushed update is green or the user explicitly overrides the gate.
- Codex must update continuity documents before ending the update.

### ChatGPT mode

Use ChatGPT mode when the assistant is operating in a regular ChatGPT chat window without direct local filesystem access.

Rules:

- ChatGPT cannot directly update the user's local repository.
- ChatGPT must work through local ZIP patch uploads unless the user explicitly chooses direct GitHub edits.
- ChatGPT update ZIPs must be patch-only packages, not rebuilt full repositories.
- Every ChatGPT update ZIP must contain exactly one named top-level folder.
- Every ChatGPT update ZIP must include an `Apply-CodeUpdate-XXXX.bat` installer and `UpdateManifest.json`.
- ChatGPT must verify the ZIP exists and has the required structure before announcing it.

## 2. Source of truth

The GitHub repository is the authoritative source of truth:

`ShisenShi1017/MajorMud-Reforged-V1.0`

Rules:

- Read the current GitHub repository state before making any update plan.
- Do not use old ZIP files, generated patches, or prior conversation memory as truth when they conflict with GitHub.
- If a local ZIP is used in ChatGPT mode, treat it only as a user-provided local-application vehicle. GitHub remains the truth after the user pushes.
- Do not continue from an unverified assumption.

## 3. Never guess

Accuracy is required.

Rules:

- Never guess method names, file paths, project state, build state, package contents, current update number, or milestone status.
- Verify before claiming something exists, passed, failed, or was changed.
- If something cannot be verified in the current environment, state exactly what was verified and exactly what remains unverified.
- Do not invent missing code, files, reports, or build results.
- Do not claim GitHub Actions is green unless GitHub or the user explicitly confirms the current pushed commit is green.

## 4. Required startup order

Every new work session must read or verify:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/Architecture/PROJECT_ROADMAP.md`
4. `README.md`
5. `VERSION.md`
6. `CHANGELOG.md`
7. `ProjectManifest.json`
8. `DEVELOPMENT_PRINCIPLES.md` when architecture, runtime, or code quality decisions are involved
9. Relevant app/source/test files for the requested change
10. Current GitHub Actions status when build verification is part of the task

## 5. Update rules

Every Code Update must:

- Confirm the current Code Update number from repository files.
- Confirm the current milestone from repository files.
- Confirm the current Database Version from repository files.
- Make the smallest safe change that satisfies the approved scope.
- Avoid gameplay work unless the user explicitly approves it.
- Preserve `Saves/`.
- Preserve `ImportedDatabases/` unless the user explicitly approves otherwise.
- Update continuity documents.
- Record verification status and known limitations.

## 6. Documentation continuity

Every Code Update must update all relevant continuity documents, including at minimum:

- `AGENTS.md` when workflow rules or current project position change
- `AI_CONTEXT.md`
- `README.md`
- `CHANGELOG.md`
- `VERSION.md`
- `ProjectManifest.json`
- `ReleaseNotes.md`
- `Docs/Architecture/PROJECT_ROADMAP.md`
- Any affected app README or report

Do not leave stale milestone, update, roadmap, source-of-truth, or next-step text behind.

## 7. Build and verification rules

- GitHub Actions is the official build gate after a push.
- Local build/test results are useful but do not replace GitHub Actions.
- Do not say an update is complete until the required local/package verification has been done.
- Do not say a pushed update is accepted until the user or GitHub status confirms green Actions for that push.

## 8. Packaging rules for ChatGPT ZIP updates

Every ChatGPT-created Code Update ZIP must:

- Be patch-only.
- Contain exactly one top-level folder.
- Include `Apply-CodeUpdate-XXXX.bat` inside that folder.
- Include `UpdateManifest.json` inside that folder.
- Include changed/new files under a `Payload/` folder using repository-relative paths.
- Automatically locate the repository root.
- Back up replaced files before copying when practical.
- Never overwrite `Saves/` or `ImportedDatabases/`.
- Be physically created and inspected before being linked.

## 9. Engineering principles

`DEVELOPMENT_PRINCIPLES.md` defines the long-term engineering standards for Reforged. Read it when making architecture, runtime, or code quality decisions.

## 10. Current project position after CodeUpdate-0135

- Current Code Update: `0135`
- Current Database Version: `0010`
- Current Milestone: `CodeUpdate0136 - Complete Classic Gameplay`
- Milestone 1: frozen
- Current priority: push CodeUpdate-0135, verify GitHub Actions green by user report, then continue CodeUpdate0136 internally without publishing partial public 0136 updates.

CodeUpdate-0135 is the verified local baseline after final exhaustive Classic fixes. It applies generated Classic bug-fix policy data, DLL-verified single-swing combat smoke support, repaired validators, duplicate trigger canonicalization, `room.unknown` runtime exclusion, and final simulation/runtime verification reports. Local Codex verification passed: exhaustive repairs validator, complete runtime verification 49/49, final simulation report 38/38, .NET solution restore/build gate, server generated-data/room-exit/NPC/combat/scripted-session smokes, compiler tests, and Core/server smoke tests. GitHub Actions remains pending until the user pushes and reports green.

CodeUpdate-0136 is the active internal milestone for complete Classic single-player gameplay. Do not create partial public CodeUpdate0136 packages. Work in internal phases until character creation/selection, inventory/equipment, complete world generation, NPC lifecycle/AI, combat rounds, magic/statuses, quests/triggers/events, shops/economy, persistence, and final exhaustive verification pass with zero executable failures and zero Classic single-player gameplay blockers. Current internal work repairs Classic kill-time drop percent rolls for orc-head-style loot: Librarium records the `1..100` roll contract and Runtime executes `roll <= drop_percent`.

CodeUpdate-0115 starts Runtime Milestone 2 with host-owned runtime state and a generic runtime event dispatcher. It adds `RuntimeStateStore`, `RuntimeEventDispatcher`, `state` and `events` runtime inspection commands, smoke coverage for state/event tracking, and `Docs/Architecture/HOST_VISION_V1.md`. Vision1 records the approved multi-world host direction: Classic, Reforged Custom, Pure AI, test realms, publishing, import wizard, world identity, transfer rules, reset system, permissions, networking, scripting boundaries, future cluster support, maintenance mode, and world health dashboard. CodeUpdate-0115 does not add combat, magic, NPC AI, spawn logic, quests, dialogue, scripts, guilds, economy integration, durable account saves, cluster runtime, maintenance mode, dashboard, formula changes, or imported/reference deletion.
CodeUpdate-0114 applies Runtime Milestone 1 server files. It adds opt-in Classic-first local console runtime, opt-in telnet/network hosting, multi-session tracking, login name prompt, world selection foundation, runtime world loading from generated `/Data`, command dispatch, scheduler shell, `Config/server.worlds.json`, Codespaces support, and restored GitHub Actions server build automation. `.github/workflows/dotnet-server.yml` now runs on every branch push, every pull request, and manual dispatch. Local .NET 8.0.422 server restore/build/bootstrap verification passed; GitHub Actions remains pending until the user pushes and reports green. It does not add combat runtime, persistent accounts/characters, saves, AI worlds, custom worlds, formula changes, imported/reference deletion, or non-Classic gameplay expansion.
CodeUpdate-0031A is a corrective build-fix patch for CodeUpdate-0031. It fixes invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs`. The user reported CodeUpdate-0031A GitHub Actions green, and Codex local .NET 8.0.422 sequential restore/build/smoke verification passed. It does not add gameplay features, networking, combat, character creation, or world simulation.

CodeUpdate-0032 improves non-gameplay Server bootstrap diagnostics by capturing ProjectManifest identity fields. The user reported CodeUpdate-0032 GitHub Actions green.

CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract. Future updates should continue recording completed work, boundaries, verification status, and next-scope candidates separately.

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. OmniVerse data may identify possible mismatches, but it must not automatically overwrite Reforged `Data/` or `ImportedDatabases/`; any correction requires a reviewed and approved follow-up update.

CodeUpdate-0035 adds Server bootstrap validation issue counts for startup output and future host tooling. It does not add gameplay, editor behavior, networking expansion, or data rewrites.

CodeUpdate-0036 adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits.

CodeUpdate-0037 combines the user-provided CodeUpdate-0036B continuity cleanup package and CodeUpdate-0037 Software Design Specification package into one documentation-only local update. It adds draft `Docs/SDS/` planning records and repairs stale continuity. It does not add gameplay, editor behavior, networking expansion, source-code behavior changes, or data rewrites.

CodeUpdate-0038 adds required-directory status reporting to the non-gameplay Server bootstrap report and startup output. It records CodeUpdate-0037 as green by user report and does not add gameplay, editor behavior, networking expansion, or data rewrites.

CodeUpdate-0039 adds a compact host-tool readiness summary to the non-gameplay Server bootstrap report and startup output. It records CodeUpdate-0038 as green by user report and keeps gameplay status explicitly disabled.

CodeUpdate-0040 preserves WCC-labeled WBB material under `ImportedDatabases/WCCLabeledWBB/` as reference/import material only.

CodeUpdate-0041 adds the read-only Classic parity audit foundation to Server bootstrap. It inventories canonical `Data/` and imported reference material but does not parse proprietary legacy formats and does not claim exact MajorMUD parity.

CodeUpdate-0042 adds read-only canonical Server data catalog loading to bootstrap. It loads registered Core data categories from `Data/`, reports category status and record counts, and keeps gameplay disabled.

CodeUpdate-0043 classifies canonical Server data categories as runtime-ready or runtime-blocked. It reports empty placeholders without failing startup or starting gameplay.

CodeUpdate-0044 adds the original reference capture/extractor tool under `Tools/ReferenceCapture/`. It preserves and inventories original/reference material, extracts ZIP files directly, can extract RAR/7z files when 7-Zip is available, captures printable strings, and writes evidence reports. It does not run original executables, does not use keygens, does not rewrite canonical `Data/`, does not delete imported/reference data, does not add gameplay, and does not claim exact MajorMUD parity.

CodeUpdate-0045 integrates the user-provided DataDecode0043 Import Readiness Validation checkpoint under `DataDecodeCheckpoints/DataDecode0043/`. DataDecode0043 estimates static binary decode at approximately 96-97%, registers 5 import blockers, reviews 1,000 foreign-key candidates, hashes 83 native source files, and promotes 0 canonical fields. It is a pre-mirror baseline only and does not change server behavior or canonical `Data/`.

CodeUpdate-0046 imports the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only. It includes WCC OmniVerse API captures for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommap files, 1,246 room detail files, a 23-entry room capture error log, the MudInfo `maps.rar` source archive, and 102 extracted MudInfo map files. HTTrack captured the OmniVerse SPA shell/assets externally under `E:\Httracks\OmniVerseCodexFull`, but the committed verification payload is the API/map evidence, not the web shell cache. Original MajorMUD binary files remain authoritative; CodeUpdate-0046 does not rewrite canonical `Data/`, promote fields, add server behavior, add gameplay, or assume OmniVerse public IDs equal binary row indexes.

CodeUpdate-0047 preserves the user-provided DataDecode0049 WGSERVER record access / structure correlation checkpoint under `DataDecodeCheckpoints/DataDecode0049/` and records the DataDecode0050 GitHub transition handoff under `DataDecodeCheckpoints/DataDecode0050/`. DataDecode0049 reports 15 NativeRuntime DAT datasets profiled, 5,916 executable strings, 9,574 record constant hits, 29 string-address xrefs, 29 record-access candidate windows, 15 descriptor candidates, 93 canonical term hits, and 0 field promotions. DataDecode0050 changes the decode workflow: future decode passes must update the GitHub repository directly, with ZIPs allowed only as archival/checkpoint artifacts. No standalone DataDecode0050 ZIP was supplied in this Codex turn; dispatcher/message items are handoff continuity only until supported by committed reports or binary-backed evidence.

CodeUpdate-0048 creates `DataDecodeCheckpoints/UnifiedDecodedData/` as a repo-native index and comparison surface for current decoded data and the CodeUpdate0046 OmniVerse/MudInfo reference payload. It adds generated inventory, category comparison reports, and an Android ChatGPT decode workflow under `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md` plus phone handoff templates. It does not move historical reports, rewrite canonical `Data/`, promote fields, add server behavior, or add gameplay.

CodeUpdate-0049 preserves the user-provided DataDecode0068 Spell Engine Proof checkpoint under `DataDecodeCheckpoints/DataDecode0068/` and refreshes `DataDecodeCheckpoints/UnifiedDecodedData/`. DataDecode0068 reports 1,180 canonical spell records, 930 native WCCSPELS records, 1,789 native WCCSPELS string rows, 1,350 OmniVerse/WCC spell reference records, 1,151 canonical `sourceId` to OmniVerse `legacy_number` matches, 41 canonical spell raw field positions, 10 reference MHT files scanned, 505 repo files with spell-term hits, 5,916 WGSERVER printable strings, 46 WGSERVER spell-term string rows, 439 WGSERVER spell constant probe rows, 8 evidence matrix rows, and 0 field promotions. DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.

CodeUpdate-0050 preserves the user-provided SimulatorBuild0002 handoff package under `DataDecodeCheckpoints/SimulatorBuild0002/`, extracts the final DataDecode0110 static certification checkpoint under `DataDecodeCheckpoints/DataDecode0110/`, adds selected DAT decoding decision docs from DataDecode0099 through DataDecode0108, and installs the expected-model simulator under `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`. DataDecode0110 certifies static `ImportedDatabases` evidence only and carries forward 3,975 unresolved ownership rows plus 5,464 unresolved static rows. SimulatorBuild0002 is useful as an expected-model server reference harness but is not live Worldgroup runtime validation. CodeUpdate-0050 excludes the embedded stale full repository archive and generated Python cache files.

CodeUpdate-0051 adds a report-only import staging readiness surface under `DataDecodeCheckpoints/ImportStaging/` and preserves the user-provided Build0021 behavioral recovery evidence package under `Docs/BehavioralRecovery/Build0021/`, `DataDecodeCheckpoints/SimulatorBuild0021/`, `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`, and `Core/Runtime/BehavioralRecovery/README.md`. Build0021 records 6 `verified_from_data`, 4 `strong_candidate`, 2 `weak_candidate`, and 4 `runtime_required` formula/catalog entries. It uses no live Worldgroup runtime validation and certifies no simulator executable formula changes. CodeUpdate-0051 does not rewrite canonical `Data/`, promote fields, add gameplay, or certify `strong_candidate` or `runtime_required` behavior.

CodeUpdate-0052 preserves the user-provided Build0027 current-state handoff under `DataDecodeCheckpoints/CurrentStateBuild0027/` and `Docs/BehavioralRecovery/Build0027/`. Build0027 records that static data/content recovery and entity/world/catalog recovery are effectively complete, but original runtime validation remains unavailable. Build0024 and Build0025 matrices remain `expected_model` until converted into actual executable simulator tests. Earlier chat pass/fail claims are not authoritative unless backed by artifact logs or locally generated test output.

CodeUpdate-0053 replaces only repository `Data/` from the embedded Patch0068 `/Data` payload. Patch0068 findings are preserved once under `DataDecodeCheckpoints/Patch0068/`, Handoff0074 is preserved once under `DataDecodeCheckpoints/Handoff0074/`, and duplicate `Docs/PatchHandoffs/` copies are intentionally not retained. Patch0071 is unreadable by the built-in ZIP reader and was not applied. CodeUpdate-0053 does not apply non-`Data` Patch0068 files, implement Patch0074 master verification, change server behavior, or add gameplay.

CodeUpdate-0054 copies only repository-new files from Validation0013 and DocUpdate0024. It adds validation/evidence/simulation/planning material, skips all existing repository paths, and does not overwrite the CodeUpdate-0053 `Data/` baseline.

CodeUpdate-0055 preserves curated current-pass Validation0043 and MegaMUD2 RE0021 evidence-only checkpoints under `DataDecodeCheckpoints/`, refreshes `DataDecodeCheckpoints/UnifiedDecodedData/`, and records the current placeholder/blocker audit. `Data/quests.json` is the only true empty canonical placeholder; `world_rooms` is a loader projection gap from `Data/world.json`. No canonical `Data/` overlay, field promotion, gameplay, server behavior change, raw packet inference, imported/reference deletion, or live runtime validation claim is included.

CodeUpdate-0057 repairs continuity after the GitHub/local `CoreUpdate 0056` commit left primary continuity files at CodeUpdate-0055. It adds the first non-gameplay modular Server foundation: a read-only game module catalog attached to Server bootstrap, with `majormud-classic` registered as a disabled data-driven module baseline and `classic` registered as a disabled rule profile. It folds Server Build Session 0001 into `Docs/ServerBuild/Session0001/` and adds `Docs/Architecture/InteractiveClientMap.md`. It does not add gameplay, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrites, imported/reference deletion, or runtime formula/timing promotion.

CodeUpdate-0058 closes the read-only `world_rooms` projection gap. `CoreDataRepositoryLoader` now loads scalar string-array collections as canonical records, and Server bootstrap attaches a read-only world projection audit. The current projection verifies 1,246 world room ids, 1,246 unique ids, 1,246 canonical rooms, 0 missing references, 0 duplicates, and start room `wcc_map1_room1` present. It does not rewrite `Data/`, start gameplay, simulate the world, add networking behavior, add live editor behavior, or promote runtime formulas/timers.

CodeUpdate-0059 adds read-only Server module readiness diagnostics. `majormud-classic` reports `host-ready-gameplay-disabled` with 1 host-ready module, 0 gameplay-enabled modules, 0 runtime rule profiles enabled, 12 runtime-ready data categories, 1 runtime-blocked category, 1 placeholder category, and 1,246 world rooms. It does not activate gameplay, rule profiles, mode/profile loading, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrites, imported/reference deletion, or runtime formula/timing promotion.

CodeUpdate-0060 adds read-only Server host configuration diagnostics. It reports safe defaults for host name, bind address, port, max sessions, world tick milliseconds, and combat tick milliseconds while keeping gameplay, networking, and world simulation disabled. It does not open sockets, start sessions, run gameplay, activate rule profiles, simulate the world, add live editor behavior, rewrite `Data/`, delete imported/reference material, or promote runtime formulas/timers.

CodeUpdate-0061 adds a read-only Server host lifecycle readiness state. It derives initialized, configured, data-ready, world-projection-ready, module-ready, and runtime-disabled lifecycle stages from existing bootstrap reports. It does not open sockets, start sessions, run gameplay, activate rule profiles, simulate the world, add live editor behavior, rewrite `Data/`, delete imported/reference material, or promote runtime formulas/timers.

CodeUpdate-0062 adds read-only Server scheduler bootstrap diagnostics and preserves curated ServerSim0036 planning/regression evidence under `Docs/ServerSim/ServerSim0036/`. Its original gameplay-facing channel names are superseded by CodeUpdate-0063. It does not start scheduler ticking, world loop, sockets, sessions, gameplay, combat, movement, spawning, status-effect ticking, script dispatch, AI, live editor behavior, `Data/` rewrites, imported/reference deletion, or runtime formula/timing promotion.

CodeUpdate-0063 corrects the CodeUpdate-0062 scheduler bootstrap surface to use generic execution channel names: Ingress, Validation, Planning, Dispatch, Mutation, TimedWork, EventWork, Persistence, Observation, and Maintenance. It also folds ServerRuntimeValidation0009 into the no-.NET simulator validation surface as expected-model lifecycle validation only. It does not start scheduler ticking, world loop, sockets, sessions, Server gameplay, combat services, movement execution, spawning services, status-effect ticking, script dispatch, AI, live editor behavior, `Data/` rewrites, imported/reference deletion, live Worldgroup parity claims, or runtime formula/timing promotion.

CodeUpdate-0064 adds a guarded scheduler/status command surface to Server bootstrap diagnostics and preserves ServerRuntimeValidation0013 movement/follow evidence under `Docs/ServerSim/ServerRuntimeValidation0013/`. It registers `status.overview`, `status.lifecycle`, `status.modules`, `status.scheduler`, `status.scheduler.channels`, `status.data`, and `status.validation` as guarded, runtime-disabled, non-mutating diagnostic command contracts. Validation0013 is partial-pass evidence only with 140 follow movement events parsed, 5 PASS, 135 REVIEW, 0 FAIL, and no canonical `Data` promotion. CodeUpdate-0064 does not add a command parser, dispatcher, scheduler ticking, world loop, sockets, sessions, Server gameplay, movement execution, party-follow implementation, combat services, spawning services, status-effect ticking, script dispatch, AI, live editor behavior, `Data/` rewrites, imported/reference deletion, live parity claims, or runtime formula/timing promotion.

CodeUpdate-0065 repairs canonical room/world data directly from `ImportedDatabases/data-v1.11n.mdb` using the repository MDB extractor path. It adds `Data/StockGameRaw/MMExplorerMDB/`, promotes `wcc_map1_room2150` as `Newhaven, Arena`, promotes `wcc_map1_room2156` as `Small Cavern`, adds both rooms to `Data/world.json`, and raises canonical/world room count from `1246` to `1248`. It promotes only direct MDB-backed fields and does not promote unknown byte fields, formulas, timing, runtime behavior, gameplay, command execution, scheduler execution, sockets, sessions, movement execution, combat, AI, or live editor behavior.

Server direction after CodeUpdate-0065: MajorMUD Classic is the first registered game module and data baseline, but MajorMUD is not the whole Server. Keep the Server generic and module-ready. Do not implement Reforged mode, Custom mode, AI systems, alternate gameplay rules, active mode/profile loading, combat, character creation, world simulation, networking expansion, command execution, scheduler execution, movement execution, party follow, or live editor behavior until explicitly approved. `Data/` defines world/content and game logic by default; Server defines reusable engine behavior, validation, execution, and enforcement; Editor should eventually edit data safely.

Engine direction after CodeUpdate-0039: favor reusable engine systems over feature-specific systems. Avoid designing around Classic unless behavior is truly Classic-specific. Game logic is data-driven by default: rules, content, progression, encounters, shops, quests, spawns, textblock actions, and world behavior should live in data unless a behavior is truly engine-internal. The server supplies reusable engine systems, validation, execution, and enforcement. The WCC default install under `ImportedDatabases/WCCDefaultInstall/` is reference/import material only.

All WCC data is reference-only unless the user explicitly approves a reviewed import/reconciliation update. MMExplorer MDB data in `ImportedDatabases/data-v1.11n.mdb` is approved for reviewed canonical repair where fields map directly, as performed in CodeUpdate-0065.

Imported/reference data may eventually be retired only after Reforged-native `Data/` is complete, verified, and explicitly approved for cleanup.

Future extraction work should remain staged as capture, decode, compare, and reviewed apply steps. Capture and decode outputs are evidence only until a separate approved reconciliation update changes canonical `Data/`.

Original MajorMUD binary files remain authoritative. Imported data, MegaMud files, OmniVerse snapshots, mudinfo references, maps, WBB comparisons, website data, community data, and tool-derived data are validation/correlation evidence only. Never assume public OmniVerse IDs equal binary row indexes. Never promote noisy raw U16 matches or candidate fields without corroborating binary evidence.

DataDecode workflow after CodeUpdate-0047: stop using checkpoint ZIPs as independent working repositories. Every future decode pass starts from the GitHub repository, commits reports/metrics/handoffs directly, preserves historical reports, and updates continuity documents. ZIPs may be exported for archival use, but GitHub remains the master copy.
