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

- `Data/` defines world/content.
- Server defines and enforces engine behavior.
- Editor should eventually edit data safely.

Future intended evolution:

1. Classic server baseline.
2. Mode/profile loader.
3. Reforged mode as Classic-derived data/rules overrides.
4. Custom/editor-created worlds.
5. AI systems and advanced behavior only after explicit approval.

## Engine Architecture Direction

Classic is the first target, but the Server is an engine, not a Classic-only server.

Favor reusable engine systems over feature-specific systems. Keep the engine generic whenever practical. Avoid naming or designing systems around Classic unless they truly are Classic-specific. World behavior should come from data whenever appropriate. Engine behavior should come from reusable systems rather than hardcoded game logic.

Future gameplay systems should plug into the engine instead of replacing it.

When designing new Server features, ask:

1. Can this be data-driven?
2. Can this be reused by future modes?
3. Can this be exposed to the Editor later?
4. Does this unnecessarily tie the engine to Classic?

Classic, Reforged, custom worlds, future expansions, and additional rule sets should not require separate server codebases. Favor extensibility over shortcuts, but avoid premature complexity.

## Current Development Position

- Current Code Update: `0051`
- Current Database Version: `0009`
- Current Milestone: `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- Milestone 1: complete/frozen
- Current priority: push CodeUpdate-0051, wait for user-reported green GitHub Actions, then create the first reviewed import diff for one high-confidence category, preferably rooms/navigation.

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

Next candidate after CodeUpdate-0051:

- Create the first reviewed import diff for one high-confidence category, preferably rooms/navigation.
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
