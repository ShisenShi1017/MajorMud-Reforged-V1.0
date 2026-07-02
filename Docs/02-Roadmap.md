# Reforged Roadmap

Last Updated: 2026-06-29

## Current Milestone

Milestone 1 - Core Framework Completion is frozen after the user reported CodeUpdate-0026B GitHub Actions green.

CodeUpdate-0024B is a corrective build-fix patch for the CoreStartupDiagnostics logger API mismatch. It does not add gameplay features.

CodeUpdate-0025 completes the active rebrand audit. No gameplay features are added.

CodeUpdate-0026 prepares the Milestone 1 finalization candidate.

CodeUpdate-0026B cleans the baseline by removing generated source-project build outputs and repairing documentation encoding.

CodeUpdate-0027 records the green CodeUpdate-0026B GitHub Actions gate and freezes the Milestone 1 baseline.

CodeUpdate-0028 records the green CodeUpdate-0027 GitHub Actions gate and defines the first Milestone 2 scope plan.

CodeUpdate-0029 implements the first approved Milestone 2 slice: host/server startup and runtime diagnostics foundation.

CodeUpdate-0030 records permanent GitHub/source-of-truth, handoff, and Codex-vs-ChatGPT workflow rules.

CodeUpdate-0031 adds the first non-gameplay Server bootstrap entry point and smoke coverage.

CodeUpdate-0031A corrects the CodeUpdate-0031 smoke-test compile failure and is green by user report.

CodeUpdate-0032 improves Server bootstrap diagnostics by capturing ProjectManifest identity fields for startup output and future handoff clarity.

CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract for host/server automation.

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. It does not rewrite Reforged data.

CodeUpdate-0035 adds Server bootstrap validation summary counts for startup output and future host tooling.

CodeUpdate-0036 adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits.

CodeUpdate-0037 combines the 0036B continuity cleanup package with the 0037 Software Design Specification package. It adds draft planning documents under `Docs/SDS/` and repairs stale continuity. It does not implement gameplay, networking, editor behavior, or data rewrites.

CodeUpdate-0038 adds required-directory status reporting to the non-gameplay Server bootstrap report and startup output.

CodeUpdate-0039 adds a compact host-tool readiness summary to Server bootstrap while keeping gameplay status disabled.

CodeUpdate-0039 also records the Classic-first Server direction: build the first real Server around the Classic baseline, keep architecture mode-ready, and do not implement Reforged mode, Custom mode, AI systems, alternate gameplay rules, or advanced behavior until explicitly approved.

CodeUpdate-0039 also records the engine-first architecture direction and imports the user-provided default WCC install into `ImportedDatabases/WCCDefaultInstall/` as reference/import material only. No canonical `Data/` rewrite is included.

CodeUpdate-0040 preserves WCC-labeled WBB material under `ImportedDatabases/WCCLabeledWBB/` as reference/import material only.

CodeUpdate-0041 adds the read-only Classic parity audit foundation to Server bootstrap. It inventories canonical `Data/` and imported reference material, but does not parse proprietary legacy formats and does not claim exact MajorMUD parity.

CodeUpdate-0042 adds read-only canonical Server data catalog loading to bootstrap. It reports registered Core data category status and record counts before gameplay exists.

CodeUpdate-0043 classifies canonical Server data categories as runtime-ready or runtime-blocked and makes empty placeholder categories visible without failing startup.

CodeUpdate-0044 adds the original reference capture/extractor tool for reviewed extraction evidence. It does not rewrite canonical `Data/`, run original executables, use keygens, or add gameplay.

CodeUpdate-0045 integrates the DataDecode0043 Import Readiness Validation checkpoint as the pre-mirror baseline. It records 96-97% static decode estimate, 5 import blockers, and 0 canonical field promotions without changing server behavior or canonical `Data/`.

CodeUpdate-0046 imports the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only. The payload includes WCC codex API JSON for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommap files, 1,246 room detail files, a 23-entry room capture error log, the MudInfo `maps.rar` source archive, and 102 extracted MudInfo map files.

CodeUpdate-0047 preserves DataDecode0049 under `DataDecodeCheckpoints/DataDecode0049/` and records the DataDecode0050 GitHub transition handoff under `DataDecodeCheckpoints/DataDecode0050/`. Future decode passes use the GitHub repository as the direct working copy; ZIPs are archival/checkpoint artifacts only.

CodeUpdate-0048 creates `DataDecodeCheckpoints/UnifiedDecodedData/` as the generated inventory/comparison surface for decoded data and the CodeUpdate0046 OmniVerse/MudInfo reference payload. It also adds Android ChatGPT decode workflow docs and phone handoff templates.

CodeUpdate-0049 preserves DataDecode0068 Spell Engine Proof under `DataDecodeCheckpoints/DataDecode0068/` and refreshes the unified decoded-data inventory to 1,701 source rows. DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.

CodeUpdate-0050 preserves SimulatorBuild0002 handoff evidence, extracts DataDecode0110 final static certification, adds selected DataDecode0099-0108 DAT decoding decision docs, and installs the expected-model simulator under `Tools/Simulator/`. SimulatorBuild0002 can help future server work as a reference harness, but it does not certify live runtime behavior.

CodeUpdate-0051 adds report-only import staging readiness reports under `DataDecodeCheckpoints/ImportStaging/` and preserves Build0021 behavioral recovery evidence under `Docs/BehavioralRecovery/Build0021/` and `DataDecodeCheckpoints/SimulatorBuild0021/`. Build0021 is evidence/catalog material only; it does not certify live runtime behavior or promote candidate formulas.

CodeUpdate-0052 preserves Build0027 current-state handoff evidence under `DataDecodeCheckpoints/CurrentStateBuild0027/` and `Docs/BehavioralRecovery/Build0027/`. Build0027 records that Build0024/0025 matrices remain `expected_model` until converted into actual executable simulator tests, and that chat pass/fail claims are not authoritative without artifact logs or locally generated test output.

CodeUpdate-0053 replaces only repository `Data/` from the embedded Patch0068 `/Data` payload, preserves Patch0068 findings under `DataDecodeCheckpoints/Patch0068/`, preserves Handoff0074 under `DataDecodeCheckpoints/Handoff0074/`, and records Patch0071 as unreadable/unapplied. Duplicate `Docs/PatchHandoffs/` copies are intentionally not retained.

CodeUpdate-0054 copies only repository-new files from Validation0013 and DocUpdate0024. It preserves the CodeUpdate-0053 `Data/` baseline by skipping all existing repository paths.

All WCC data is reference-only unless the user explicitly approves a reviewed import/reconciliation update.

Imported/reference data may eventually be retired only after Reforged-native `Data/` is complete, verified, and explicitly approved for cleanup.


## Development Phases

### Phase 1: Standalone Server

Primary goal: stable standalone Reforged server.

Planned focus areas:

- Server startup/shutdown stability.
- Character creation and login.
- Save/load reliability.
- Room movement.
- Commands.
- Combat.
- Monsters.
- Items.
- Shops.
- Spells and skills.
- Admin tools.
- Imported database support.

### Phase 2: Host/World Editor

Primary goal: safe editor for game content.

Planned focus areas:

- Data validation.
- Room editor.
- Monster editor.
- Item editor.
- Race/class editor.
- Spell/skill editor.
- Message editor.
- Import/export tooling.
- Documentation of database fields.

### Phase 3: Client Work

Primary goal: modern client inspired by companion client 2.0.

Planned focus areas:

- PC client improvements.
- Android client exploration.
- Mapping.
- Triggers.
- Aliases.
- Macros.
- Profiles.
- Scripting.
- UI themes.

## Roadmap Rule

The roadmap is a planning document, not an implementation claim. Items listed here are not complete unless recorded in release notes or patch history.

## Milestone 2 Preview

Milestone 2 should focus on engine modernization, host/world editor planning, server improvements, data validation tools, runtime performance, gameplay systems, and client/editor modernization. Begin implementation only after the user approves the Milestone 2 starting scope.

First implementation slice: CodeUpdate-0029 wires Server startup through existing Core configuration, logging, repository health, event bus, and startup diagnostics without adding gameplay systems.

Current implementation slice: CodeUpdate-0054 records the Validation0013 and DocUpdate0024 new-file-only intake. Next work should build a focused verification index over Validation0013 evidence and current `Data/` before further server behavior or executable harness work.
