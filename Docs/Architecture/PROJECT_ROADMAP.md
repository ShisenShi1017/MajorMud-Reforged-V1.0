# Reforged Development Roadmap and Project Constitution

## READ THIS FIRST

Every AI assistant, Codex session, ChatGPT session, or developer joining this project must read this document before changing anything.

Required startup order:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/Architecture/PROJECT_ROADMAP.md`
4. `README.md`
5. `VERSION.md`
6. `CHANGELOG.md`
7. `ProjectManifest.json`
8. `DEVELOPMENT_PRINCIPLES.md`
9. Relevant source/test/application docs for the requested change

## Source of Truth

The GitHub repository is the authoritative source of truth:

`ShisenShi1017/MajorMud-Reforged-V1.0`

Do not use old ZIPs, prior patches, or conversation memory as truth when they conflict with GitHub. In ChatGPT mode, ZIPs are local-application vehicles only. In Codex mode, local work must be pushed and verified green in GitHub Actions before the next update continues.

## Never Guess Rule

Never guess when creating, fixing, packaging, reviewing, or reporting updates.

- Verify current code before changing it.
- Verify file paths before referencing them.
- Verify package contents before linking them.
- Verify build/test/GitHub Actions status before claiming success.
- If verification is not possible in the current environment, state exactly what was and was not verified.

## Work Modes

### Codex mode

- Codex updates local repository files only.
- Codex must stop after each Code Update until the user pushes to GitHub and states the current push has green GitHub Actions.
- Codex must not move to the next update until that green gate is confirmed or the user explicitly overrides the gate.

### ChatGPT mode

- ChatGPT cannot directly update the user's local repository.
- ChatGPT uses patch-only ZIP updates for local application unless the user explicitly requests direct GitHub edits.
- Every ChatGPT ZIP must contain exactly one top-level folder, an apply BAT, and an update manifest.
- The ZIP must be verified before delivery.

## Project Vision

Reforged is not a runtime-dependent copy of the original legacy MUD. It is a clean, standalone game engine and toolset inspired by legacy MUD. Original legacy MUD data may be imported, decoded, compared, and audited, but after import the project must operate from its own simple, editable `Data/` files.

The long-term goal is a standalone server, native importer, live editor, launcher, Windows client, Android client, gameplay systems, and plugin framework that do not require the original legacy MUD runtime.

## Server Mode Direction

The first real Server target is the Classic baseline. The Server should load and run Classic data first.

Do not implement Reforged mode, Custom mode, AI systems, alternate gameplay rules, or advanced behavior until explicitly approved.

Architecture should remain mode-ready without hardcoding Classic in a way that blocks future modes. Add only simple extension points needed for future mode/profile loading until the Classic baseline is working.

Responsibility split:

- `Data/` defines world/content and game logic by default.
- Server defines reusable engine behavior, validation, execution, and enforcement.
- Editor should eventually edit data safely.

Future intended evolution:

1. Classic server baseline.
2. Mode/profile loader.
3. Reforged mode as Classic-derived data/rules overrides.
4. Custom/editor-created worlds.
5. AI systems and advanced behavior only after explicit approval.

## Engine Architecture Direction

Classic is the first target, but the Server is an engine, not a Classic-only server.

Favor reusable engine systems over feature-specific systems. Keep the engine generic whenever practical. Avoid naming or designing systems around Classic unless they truly are Classic-specific. Game logic is data-driven by default: rules, content, progression, encounters, shops, quests, spawns, textblock actions, and world behavior should live in data unless a behavior is truly engine-internal. Engine behavior should come from reusable systems rather than hardcoded game-specific logic.

Future gameplay systems should plug into the engine instead of replacing it.

When designing new Server features, ask:

1. Is this implemented through data unless it is truly engine-internal?
2. Can this be reused by future modes?
3. Can this be exposed to the Editor later?
4. Does this unnecessarily tie the engine to Classic?

Classic, Reforged, custom worlds, future expansions, and additional rule sets should not require separate server codebases. Favor extensibility over shortcuts, but avoid premature complexity.

## Current Development Position

- Current Code Update: `0064`
- Current Database Version: `0009`
- Current Milestone: `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- Milestone 1: complete/frozen
- Current priority: push CodeUpdate-0064 when ready, verify GitHub Actions green by user report, then continue with one narrow non-gameplay Server construction slice.

CodeUpdate-0031A corrects the CodeUpdate-0031 GitHub Actions build failure in `Tests/Source/Reforged.Core.Tests/Program.cs`. The fix is limited to the invalid C# string literal in the configuration smoke test. The user reported CodeUpdate-0031A GitHub Actions green, and Codex local .NET 8.0.422 sequential restore/build/smoke verification passed.

CodeUpdate-0032 improves Server bootstrap diagnostics by capturing ProjectManifest identity fields for startup output and smoke-test verification. The user reported CodeUpdate-0032 GitHub Actions green.

CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract for future host/server automation.

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. It does not rewrite `Data/` or overwrite `ImportedDatabases/`.

CodeUpdate-0035 adds Server bootstrap validation issue counts for startup output and future host tooling.

CodeUpdate-0036 adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits.

CodeUpdate-0037 combines the 0036B continuity cleanup package with the 0037 Software Design Specification package. It adds draft `Docs/SDS/` planning records and repairs stale continuity. It does not implement runtime systems.

CodeUpdate-0038 adds required-directory status records and counts to the non-gameplay Server bootstrap report and startup output.

CodeUpdate-0039 adds a compact host-tool readiness summary while keeping gameplay status disabled.

CodeUpdate-0039 also records the Classic-first Server direction: Classic baseline first, mode-ready architecture later, no Reforged/Custom/AI/alternate gameplay behavior until explicitly approved.

CodeUpdate-0039 also records the engine-first architecture direction and imports the user-provided WCC default install into `ImportedDatabases/WCCDefaultInstall/` as reference/import material only.

CodeUpdate-0040 preserves WCC-labeled WBB material under `ImportedDatabases/WCCLabeledWBB/` as reference/import material only.

CodeUpdate-0041 adds the read-only Classic parity audit foundation to Server bootstrap. It inventories canonical `Data/` and imported reference material, but does not parse proprietary legacy formats and does not claim exact MajorMUD parity.

CodeUpdate-0042 adds read-only canonical Server data catalog loading to bootstrap. It reports registered Core data category status and record counts before gameplay exists.

CodeUpdate-0043 classifies canonical Server data categories as runtime-ready or runtime-blocked and makes empty placeholder categories visible without failing startup.

CodeUpdate-0044 adds the original reference capture/extractor tool for reviewed extraction evidence. It preserves source material, inventories files and strings, and keeps all output reference-only until a separate reviewed reconciliation update.

CodeUpdate-0045 integrates the DataDecode0043 Import Readiness Validation checkpoint under `DataDecodeCheckpoints/DataDecode0043/`. It records 96-97% static decode estimate, 5 import blockers, 1,000 reviewed foreign-key candidates, 83 native source files hashed, and 0 canonical field promotions.

CodeUpdate-0046 imports the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only. The payload includes WCC OmniVerse API captures for major codex categories, 168 shop-seeded roommap files, 1,246 room detail files, a 23-entry room capture error log, the MudInfo `maps.rar` source archive, and 102 extracted MudInfo map files. The OmniVerse SPA shell/assets were captured externally by HTTrack, but they are not the committed verification payload.

CodeUpdate-0047 preserves the user-provided DataDecode0049 WGSERVER record access / structure correlation checkpoint and records the DataDecode0050 GitHub transition handoff. Future decode passes must update the GitHub repository directly. ZIPs may still be exported as archival/checkpoint artifacts, but they are no longer the primary working source of truth.

CodeUpdate-0048 creates `DataDecodeCheckpoints/UnifiedDecodedData/` as a generated decoded-data inventory and OmniVerse comparison surface. It also adds Android ChatGPT handoff workflow docs for phone-based decode work where PowerShell is unavailable.

CodeUpdate-0049 preserves the user-provided DataDecode0068 Spell Engine Proof checkpoint under `DataDecodeCheckpoints/DataDecode0068/` and refreshes the unified decoded-data inventory. DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.

CodeUpdate-0050 preserves the user-provided SimulatorBuild0002 handoff, extracts DataDecode0110 final static certification, adds selected DataDecode0099-0108 DAT decoding decision docs, and installs the expected-model simulator. SimulatorBuild0002 is useful as a server reference harness, but it is not live Worldgroup runtime validation.

CodeUpdate-0051 adds report-only import staging readiness reports and preserves the user-provided Build0021 behavioral recovery evidence package. Build0021 records 6 `verified_from_data`, 4 `strong_candidate`, 2 `weak_candidate`, and 4 `runtime_required` formula/catalog entries. It does not use live Worldgroup runtime validation and does not certify simulator executable formula updates.

CodeUpdate-0052 preserves the user-provided Build0027 current-state handoff under `DataDecodeCheckpoints/CurrentStateBuild0027/` and `Docs/BehavioralRecovery/Build0027/`. Build0027 records that static data/content recovery and entity/world/catalog recovery are effectively complete, while original runtime validation remains unavailable. Build0024 and Build0025 matrices remain `expected_model` until converted into actual executable simulator tests.

CodeUpdate-0053 replaces only repository `Data/` from the embedded Patch0068 `/Data` payload, preserves Patch0068 findings under `DataDecodeCheckpoints/Patch0068/`, preserves Handoff0074 under `DataDecodeCheckpoints/Handoff0074/`, and records Patch0071 as unreadable/unapplied. Duplicate `Docs/PatchHandoffs/` copies are intentionally not retained. Server data-readiness diagnostics and smoke expectations were updated for the populated data baseline. No non-`Data` Patch0068 overlay, gameplay server behavior change, gameplay system, or Patch0074 master verification implementation is included.

CodeUpdate-0054 copies only repository-new files from the Validation0013 cumulative package and DocUpdate0024 Librarium planning package. It adds 437 new Validation0013 files and 2 new DocUpdate0024 files while skipping 86 existing Validation0013 paths without overwriting them. It does not replace the CodeUpdate-0053 `Data/` baseline, promote fields, add gameplay, or claim live runtime validation.

CodeUpdate-0055 preserves curated current-pass Validation0043 and MegaMUD2 RE0021 evidence-only checkpoints under `DataDecodeCheckpoints/`, refreshes `DataDecodeCheckpoints/UnifiedDecodedData/`, and records the current placeholder/blocker audit. `Data/quests.json` is the only true empty canonical placeholder, and `world_rooms` is a server catalog projection gap from `Data/world.json`. Validation0043 contributes matched textblock action dispatcher/opcode evidence; MegaMUD2 RE0021 contributes route/client/protocol text-command evidence. No canonical `Data/` overlay, field promotion, gameplay system, server behavior change, raw packet inference, imported/reference deletion, or live runtime validation claim is included.

CodeUpdate-0057 repairs continuity after GitHub/local `CoreUpdate 0056` left primary manifests and continuity documents at CodeUpdate-0055. It begins server construction with a non-gameplay modular Server foundation: Server bootstrap now attaches a read-only game module catalog, registers `majormud-classic` as the first disabled data-driven module baseline, and registers a disabled `classic` rule profile. It also folds Server Build Session 0001 into `Docs/ServerBuild/Session0001/` and adds the interactive client map planning placeholder under `Docs/Architecture/InteractiveClientMap.md`. No gameplay, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrite, imported/reference deletion, or runtime formula/timing promotion is included.

CodeUpdate-0058 closes the read-only `world_rooms` projection gap. `CoreDataRepositoryLoader` now loads scalar string-array collections as canonical records, and Server bootstrap attaches a read-only world projection audit. The current projection verifies 1,246 world room ids, 1,246 unique ids, 1,246 canonical rooms, 0 missing references, 0 duplicates, and start room `wcc_map1_room1` present. No canonical `Data/` rewrite, gameplay, combat, character creation, world simulation, networking expansion, live editor behavior, imported/reference deletion, or runtime formula/timing promotion is included.

CodeUpdate-0059 adds read-only Server module readiness diagnostics. `majormud-classic` reports `host-ready-gameplay-disabled` with 1 host-ready module, 0 gameplay-enabled modules, 0 runtime rule profiles enabled, 12 runtime-ready data categories, 1 runtime-blocked category, 1 placeholder category, and 1,246 world rooms. No gameplay, runtime rule execution, mode/profile activation, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrite, imported/reference deletion, or runtime formula/timing promotion is included.

CodeUpdate-0060 adds read-only Server host configuration diagnostics. It reports safe host defaults and optional `Config/server.host.json` presence while keeping gameplay, networking, and world simulation disabled. No sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrite, imported/reference deletion, or runtime formula/timing promotion is included.

CodeUpdate-0061 adds read-only Server host lifecycle readiness diagnostics. It derives initialized, configured, data-ready, world-projection-ready, module-ready, and runtime-disabled stages from existing bootstrap reports. No sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrite, imported/reference deletion, or runtime formula/timing promotion is included.

CodeUpdate-0062 adds read-only Server scheduler bootstrap diagnostics. It registers ten disabled diagnostic channels and preserves curated ServerSim0036 planning evidence under `Docs/ServerSim/ServerSim0036/`. No scheduler ticking, sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat, movement, spawning, status-effect ticking, script dispatch, AI, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrite, imported/reference deletion, or runtime formula/timing promotion is included.

CodeUpdate-0063 corrects CodeUpdate-0062 scheduler bootstrap diagnostics to use generic execution channel names instead of gameplay-specific names, preserves curated DataSimVer0045 evidence-regression contract planning under `Docs/ServerSim/DataSimVer0045/`, and folds ServerRuntimeValidation0009 into the no-.NET simulator validation surface under `Docs/ServerSim/ServerRuntimeValidation0009/`. The generic channels are Ingress, Validation, Planning, Dispatch, Mutation, TimedWork, EventWork, Persistence, Observation, and Maintenance. No scheduler ticking, sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat services, movement execution, spawning services, status-effect ticking, script dispatch, AI, character creation, execution-event adapter, replay harness, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrite, imported/reference deletion, live parity claim, or runtime formula/timing promotion is included.

CodeUpdate-0064 adds guarded scheduler/status command diagnostics to Server bootstrap and preserves ServerRuntimeValidation0013 movement/follow evidence under `Docs/ServerSim/ServerRuntimeValidation0013/`. Registered status commands are `status.overview`, `status.lifecycle`, `status.modules`, `status.scheduler`, `status.scheduler.channels`, `status.data`, and `status.validation`. Every command is guarded, runtime-disabled, and non-mutating. No command parser, dispatcher, scheduler ticking, sockets, sessions, gameplay, movement execution, party-follow implementation, runtime rule execution, mode/profile activation, combat services, spawning services, status-effect ticking, script dispatch, AI, character creation, world simulation, networking expansion, live editor behavior, canonical `Data/` rewrite, imported/reference deletion, live parity claim, or runtime formula/timing promotion is included.

## Milestone 1 - Core Framework (Frozen)

Goal: Build the shared foundation that every application depends on.

Status: Complete/frozen after the user-reported green GitHub Actions freeze sequence.

Deliverables include Core configuration, logging, validation, serialization, repository interfaces, shared data models, event system, service registry, version management, manifest reading, repository health checks, shared utilities, smoke-test foundation, and application Core references.

## Milestone 2 - Engine Modernization / Host & World Editor

Goal: Modernize the engine host path and prepare safe host/world editor workflows while preserving the verified Milestone 1 Core baseline.

Status: Active.

Completed/started slices:

- CodeUpdate-0029: startup-only Server diagnostics.
- CodeUpdate-0030: governance/source-of-truth/handoff cleanup.
- CodeUpdate-0031: non-gameplay Server bootstrap foundation.
- CodeUpdate-0031A: corrective smoke-test compile fix for the 0031 GitHub Actions failure.
- CodeUpdate-0032: Server bootstrap manifest identity output.
- CodeUpdate-0033: Server bootstrap exit-code contract.
- CodeUpdate-0034: OmniVerse read-only reference audit policy.
- CodeUpdate-0035: Server bootstrap validation summary counts.
- CodeUpdate-0036: knowledge-base and design-decision documentation.
- CodeUpdate-0037: Software Design Specification draft planning records and continuity cleanup.
- CodeUpdate-0038: Server bootstrap required-directory status reporting.
- CodeUpdate-0039: Server bootstrap host-tool readiness summary.
- CodeUpdate-0040: WCC-labeled WBB reference/import material preservation.
- CodeUpdate-0041: read-only Classic parity audit foundation.
- CodeUpdate-0042: Server data catalog bootstrap readiness.
- CodeUpdate-0043: Server data readiness classification.
- CodeUpdate-0044: Original reference capture/extractor tool.
- CodeUpdate-0045: DataDecode0043 checkpoint integration.
- CodeUpdate-0046: OmniVerse/MudInfo capture checkpoint.
- CodeUpdate-0047: DataDecode GitHub transition and DataDecode0049 preservation.
- CodeUpdate-0048: Unified decoded-data inventory, OmniVerse comparison, and Android ChatGPT decode workflow.
- CodeUpdate-0049: DataDecode0068 Spell Engine Proof preservation.
- CodeUpdate-0050: SimulatorBuild0002 handoff and DataDecode0110 static certification preservation.
- CodeUpdate-0051: Import staging readiness reports and Build0021 behavioral recovery evidence preservation.
- CodeUpdate-0052: Build0027 current-state handoff preservation.
- CodeUpdate-0053: Patch0068 embedded `Data/` replacement and Handoff0074/Patch0068 evidence intake.
- CodeUpdate-0054: Validation0013 and DocUpdate0024 new-file-only evidence/planning intake.
- CodeUpdate-0055: Validation0043 and MegaMUD2 RE0021 evidence preservation, UnifiedDecodedData refresh, and current placeholder/blocker audit.
- CodeUpdate-0057: modular Server foundation and 0056 continuity repair.
- CodeUpdate-0058: world projection index and scalar string-array loader support.
- CodeUpdate-0059: module readiness report with gameplay/rule profiles disabled.
- CodeUpdate-0060: host configuration surface with runtime toggles disabled.
- CodeUpdate-0061: host lifecycle readiness state with runtime disabled.
- CodeUpdate-0062: scheduler bootstrap diagnostics with runtime disabled.
- CodeUpdate-0063: generic scheduler execution channels with runtime disabled, plus no-.NET simulator lifecycle validation folded from ServerRuntimeValidation0009.
- CodeUpdate-0064: guarded scheduler/status command diagnostics with command execution disabled.

Focus Areas:

- Server improvements.
- Host/world editor planning and later implementation.
- Data validation tools.
- Runtime performance.
- Client/editor modernization.
- Gameplay systems only after explicit approval.

Boundaries until approved:

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No live editor implementation beyond approved scope.
- No Reforged mode, Custom mode, AI systems, alternate gameplay rules, or mode/profile loader implementation.
- No `Data/` rewrite from imported WCC reference material.
- All WCC data remains reference-only unless the user explicitly approves a reviewed import/reconciliation update.
- Imported/reference data may be retired only after Reforged-native `Data/` is complete, verified, and explicitly approved for cleanup.

Next candidate after CodeUpdate-0064:

- Choose one narrow non-gameplay Server construction slice.
- Prefer a read-only status output formatter or query adapter before adding gameplay behavior.
- Keep Validation0043 and MegaMUD2 RE0021 as evidence unless a separate reviewed reconciliation update promotes a specific field or behavior.
- Keep live runtime behavior uncertified until controlled Worldgroup validation is available.
- Preserve previous decode reports instead of overwriting historical analysis.
- Promote no fields without binary evidence.

## Milestone 3 - Standalone Server

Goal: Build the standalone game server.

Features:

- Load `Data/`.
- World initialization.
- Room manager.
- Item manager.
- Monster manager.
- Spell manager.
- Shop manager.
- Player manager.
- Save system.
- Main game loop.
- Session manager.
- Network framework.
- Command dispatcher.
- Validation before startup.

Completion Criteria:

- Server boots successfully using only Reforged data.

## Later Milestones

- Milestone 4: Live Editor.
- Milestone 5: Launcher.
- Milestone 6: Windows Client.
- Milestone 7: Android Client.
- Milestone 8: Gameplay Systems.
- Milestone 9: Plugin Framework.
- Milestone 10: Version 1.0.

## Permanent Project Rules

- GitHub is the source of truth.
- `Data/` is the authoritative game database after import.
- `ImportedDatabases/` remains archive/reference only.
- Every update inspects repository truth before making changes.
- Every update updates all relevant continuity documents.
- Every ChatGPT update ZIP includes a self-contained `Apply-*.bat` until the Launcher replaces it.
- Every update automatically locates the project root.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Never claim an update package exists unless it has actually been created and verified.
- Repository layout is frozen unless the user explicitly approves a structural change.
- Runtime must never require the .NET SDK.
- All approved suggestions must be implemented in the next applicable update unless explicitly deferred or rejected.
