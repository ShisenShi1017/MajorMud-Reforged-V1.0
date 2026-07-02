# Reforged

> A modern standalone reimplementation of Reforged designed for long-term maintainability, live editing, and future cross-platform support.

## Current Project State

- **Current Code Update:** `CodeUpdate-0053 - Patch0068 Data Replacement and Handoff Intake`
- **Current Database Version:** `DatabaseVersion-0009 - Server-Ready Data Baseline`
- **Active Milestone:** `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- **Current Priority:** Push CodeUpdate-0053, wait for green GitHub Actions, then run a focused verification/reconciliation pass over the new `Data/` baseline.

This README is a required starting point for every new ChatGPT, Codex, or developer handoff session.

## Required Startup Order

Every session must read these files before changing the repository:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/Architecture/PROJECT_ROADMAP.md`
4. `README.md`
5. `VERSION.md`
6. `CHANGELOG.md`
7. `ProjectManifest.json`
8. `DEVELOPMENT_PRINCIPLES.md`
9. Relevant source, test, and application documentation for the requested change

## CodeUpdate-0053 Summary

CodeUpdate-0053 replaces only repository `Data/` from the embedded Patch0068 `/Data` payload.

It preserves Patch0068 findings under `DataDecodeCheckpoints/Patch0068/` and Handoff0074 under `DataDecodeCheckpoints/Handoff0074/`. Duplicate `Docs/PatchHandoffs/` copies are intentionally not retained.

Patch0068 contributed 86 embedded `Data/` files, including populated rooms, room graph, shops, loot tables, abilities, message/text catalogs, behavioral runtime evidence, and provenance material. Patch0071 was inspected by hash but could not be opened by the built-in ZIP reader, so it remains unapplied.

Server data-readiness diagnostics and smoke expectations were updated so populated Patch0068 data categories are no longer treated as empty placeholders.

No non-`Data` Patch0068 overlay, Patch0071 application, Patch0074 master verification implementation, gameplay server behavior change, gameplay system, imported/reference deletion, or live runtime validation claim is included.

## CodeUpdate-0052 Summary

CodeUpdate-0052 preserves the user-provided Build0027 current-state handoff.

It adds the full handoff under `DataDecodeCheckpoints/CurrentStateBuild0027/` and a human-readable copy under `Docs/BehavioralRecovery/Build0027/`.

Build0027 records that static data/content recovery and entity/world/catalog recovery are effectively complete, while original runtime validation remains unavailable. Build0024 and Build0025 matrices remain `expected_model` until converted into actual executable simulator tests. Prior chat pass/fail claims are not authoritative unless backed by artifact logs or locally generated test output.

No executable harness implementation, canonical `Data/` rewrite, field promotion, server behavior change, gameplay system, or original runtime behavior certification is included.

## CodeUpdate-0051 Summary

CodeUpdate-0051 adds a report-only import staging readiness surface and preserves the user-provided Build0021 behavioral recovery evidence package.

It adds `Tools/DataDecode/Build-ImportStagingReport.ps1` and generates `DataDecodeCheckpoints/ImportStaging/` with canonical data surface reports, simulator evidence reports, a canonical import staging queue, and a phone handoff template for Android ChatGPT decode work.

It also preserves Build0021 under `Docs/BehavioralRecovery/Build0021/` and `DataDecodeCheckpoints/SimulatorBuild0021/`, adds `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`, and adds `Core/Runtime/BehavioralRecovery/README.md`.

Build0021 records 6 `verified_from_data`, 4 `strong_candidate`, 2 `weak_candidate`, and 4 `runtime_required` formula/catalog entries. It uses no live Worldgroup runtime validation and certifies no simulator executable formula changes.

No canonical `Data/` rewrite, field promotion, server behavior change, gameplay system, live runtime validation claim, or Build0021 formula certification is included.

## CodeUpdate-0050 Summary

CodeUpdate-0050 preserves the user-provided SimulatorBuild0002 handoff package.

It adds `DataDecodeCheckpoints/SimulatorBuild0002/`, extracts final `DataDecode0110` static certification files under `DataDecodeCheckpoints/DataDecode0110/`, adds selected DAT decoding decision docs, and installs the expected-model simulator under `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`.

DataDecode0110 reports static `ImportedDatabases` certification only: 3,908 imported database files checked, 1 MDB present, 5 CSV files present, 0 unsupported inferences, 3,975 unresolved ownership rows carried forward, and 5,464 unresolved static rows carried forward. Runtime validation is still not started.

SimulatorBuild0002 can help server work as an expected-model reference harness for room lookup, navigation, population, shops, drops, NPC/dialogue search, spells, skills, and placeholder combat scenarios. It does not certify live Worldgroup behavior or exact combat formulas, timers, scripts, AI, drop rates, shop restock behavior, or quest state mutation.

No canonical `Data/` rewrite, server behavior change, gameplay system, field promotion, live runtime validation, or embedded stale full-repo archive is included.

## CodeUpdate-0049 Summary

CodeUpdate-0049 preserves the user-provided DataDecode0068 Spell Engine Proof checkpoint under `DataDecodeCheckpoints/DataDecode0068/`.

DataDecode0068 reports 1,180 canonical spell records, 930 native WCCSPELS records, 1,789 native WCCSPELS string rows, 1,350 OmniVerse/WCC spell reference records, 1,151 canonical `sourceId` to OmniVerse `legacy_number` matches, 41 canonical spell raw field positions, 10 reference MHT files scanned, 505 repo files with spell-term hits, 5,916 WGSERVER printable strings, 46 WGSERVER spell-term string rows, 439 WGSERVER spell constant probe rows, 8 evidence matrix rows, and 0 field promotions.

The unified decoded-data inventory was regenerated and now records 1,701 source inventory rows. DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.

No canonical `Data/` rewrite, spell field promotion, server behavior change, gameplay system, executable execution, or OmniVerse-only promotion is included.

## CodeUpdate-0048 Summary

CodeUpdate-0048 creates `DataDecodeCheckpoints/UnifiedDecodedData/` as a single repo-native place to index current decoded data and compare it against the CodeUpdate0046 OmniVerse/MudInfo reference payload.

The generated comparison confirms complete normalized-name coverage for classes and races, partial spell coverage by `sourceId` to OmniVerse `legacy_number`, item name-only correlation because canonical items lack `legacy_number`, and clear canonical gaps for shops, rooms, and full monster parity.

It also adds `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md` and phone handoff templates so Android ChatGPT decode work can produce structured handoffs without PowerShell.

No canonical `Data/` rewrite, field promotion, server behavior change, gameplay system, or imported/reference deletion is included.

## CodeUpdate-0047 Summary

CodeUpdate-0047 moves decode continuity into the repository as the authoritative working copy.

It preserves the user-provided DataDecode0049 WGSERVER record access / structure correlation package under `DataDecodeCheckpoints/DataDecode0049/` and records the DataDecode0050 GitHub transition handoff under `DataDecodeCheckpoints/DataDecode0050/`.

DataDecode0049 reports 15 NativeRuntime DAT datasets profiled, 5,916 executable ASCII strings, 9,574 record constant hits, 29 string-address xrefs, 29 record-access candidate windows, 15 descriptor candidates, 93 canonical term hits, and 0 field promotions.

Future decode passes must update the GitHub repository directly. ZIPs may still be exported as archival/checkpoint artifacts, but they are no longer the primary working source of truth.

No canonical `Data/` rewrite, field promotion, server behavior change, gameplay system, `ImportedDatabases/` deletion, or executable execution is included.

## CodeUpdate-0046 Summary

CodeUpdate-0046 records and imports the current OmniVerse/MudInfo reference capture state under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/`.

HTTrack captured the OmniVerse SPA shell/assets under `E:\Httracks\OmniVerseCodexFull`, but HTTrack alone did not capture full codex data because OmniVerse loads records through API calls. Direct WCC OmniVerse API captures are preserved in the imported reference payload for abilities, classes, items, monsters, races, shops, spells, world status, roommaps, and room details.

MudInfo `maps.rar` was found, hashed, listed with 7-Zip, and preserved with 102 extracted map files as map/layout correlation evidence only.

Room endpoint discovery is now partially captured through shop-seeded roommap pulls: 168 roommap files, 1,246 room detail files, and a 23-entry room detail error log. The next data step is binary-first verification and expanded room coverage without assuming public IDs equal binary row indexes.

## CodeUpdate-0045 Summary

CodeUpdate-0045 integrates the user-provided DataDecode0043 Import Readiness Validation checkpoint under `DataDecodeCheckpoints/DataDecode0043/`.

DataDecode0043 is the pre-mirror import-readiness baseline. It reports an approximately 96-97% static binary decode estimate, 6 canonical schemas checked, 16 repository JSON datasets checked, 1,000 foreign-key candidates reviewed, 83 native source files hashed, 5 import blockers registered, and 0 canonical field promotions.

This checkpoint does not change canonical `Data/`, server behavior, gameplay, networking, editor behavior, or import state. Original MajorMUD binary files remain authoritative. OmniVerse snapshots, MegaMud files, mudinfo references, maps, WBB comparisons, website data, community data, and tool-derived data remain validation/correlation evidence only.

## CodeUpdate-0044 Summary

CodeUpdate-0044 adds `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1`, a reference-only extraction/capture tool for original Worldgroup, WCC, WBB, WSGSERVE, and related source material.

The tool preserves source archives under named `ImportedDatabases/ReferenceCaptures/` folders, extracts ZIP files directly, extracts RAR/7z files when 7-Zip is available, inventories files and hashes, records category hints, captures printable strings, and writes reports under named `Reports/ReferenceCaptures/` folders.

This is extraction evidence only. It does not run original executables, does not use keygens, does not bypass licensing, does not rewrite canonical `Data/`, does not delete imported material, and does not add gameplay.

## CodeUpdate-0043 Summary

CodeUpdate-0043 classifies canonical Server data readiness separately from file presence. Server startup now reports runtime-ready categories, runtime-blocked categories, empty placeholders, and category-specific readiness notes.

Current local bootstrap reports 13/13 canonical categories present, 8 runtime-ready categories, 5 runtime-blocked placeholder categories, and 4,527 loaded records. Gameplay remains disabled.

Long term, imported/reference data should be retired only after Reforged-native `Data/` is complete, decoded/imported or recreated, verified by reports, and explicitly approved for cleanup.

## CodeUpdate-0042 Summary

CodeUpdate-0042 adds canonical server data catalog loading to the non-gameplay Server bootstrap path. Startup now loads the registered Core `Data/` categories, reports category/record readiness, and keeps gameplay disabled.

Current local bootstrap loads 13/13 canonical categories and 4,527 records. It also reports 5 empty placeholder categories: loot tables, quests, rooms, shops, and trainers.

No gameplay systems, world simulation, networking expansion, data rewrite, WCC import, or parity claim is added.

## CodeUpdate-0041 Summary

CodeUpdate-0041 adds the first read-only Classic parity audit foundation. Server bootstrap now inventories canonical `Data/` and imported reference material, prints broad Classic category status, and keeps gameplay disabled.

This does not parse proprietary legacy binary formats yet, does not rewrite `Data/`, and does not claim exact MajorMUD parity.

## CodeUpdate-0040 Summary

CodeUpdate-0040 preserves the user-provided WCC-labeled WBB archive under `ImportedDatabases/WCCLabeledWBB/` as reference/import material only.

All WCC data and WCC-labeled data is reference-only unless the user explicitly approves a reviewed import or reconciliation update.

## CodeUpdate-0039 Summary

CodeUpdate-0039 improves the non-gameplay Server bootstrap by exposing a compact host-tool readiness summary. Startup output now states whether the repo is ready for host/editor tooling while keeping gameplay status explicitly disabled.

CodeUpdate-0038 GitHub Actions is green by user report.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0038 Summary

CodeUpdate-0038 improves the non-gameplay Server bootstrap by exposing required-directory status records and counts in the bootstrap report and startup output. This helps future host/editor tooling inspect repository readiness without parsing validation errors.

CodeUpdate-0038 GitHub Actions is green by user report.

CodeUpdate-0037 GitHub Actions is green by user report.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0037 Summary

CodeUpdate-0037 is a documentation-only update that combines the user-provided CodeUpdate-0036B continuity cleanup package with the user-provided CodeUpdate-0037 Software Design Specification package. It repairs stale continuity after the direct-GitHub CodeUpdate-0036 documentation commits and adds draft `Docs/SDS/` planning records for future systems.

CodeUpdate-0037 GitHub Actions is green by user report.

No source-code behavior, data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0036 Summary

CodeUpdate-0036 adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits.

No source-code behavior, data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0035 Summary

CodeUpdate-0035 improves the non-gameplay Server bootstrap by exposing validation issue counts and printing a startup validation summary by severity. It keeps the work on the pre-website host/server track.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0034 Summary

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. OmniVerse may help identify possible mismatches in Reforged data, but it must not automatically overwrite canonical `Data/` or archive `ImportedDatabases/`.

CodeUpdate-0034 GitHub Actions is green by user report.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0033 Summary

CodeUpdate-0033 improves the non-gameplay Server bootstrap by making exit-code behavior an explicit report contract. Server startup returns the report exit code, and smoke tests verify success exposes exit code 0 while failure exposes exit code 1.

CodeUpdate-0033 GitHub Actions is green by user report.

No gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0032 Summary

CodeUpdate-0032 improves the non-gameplay Server bootstrap by capturing ProjectManifest identity fields and printing them during startup diagnostics: project, CodeUpdate, database version, and milestone. It also adds smoke-test coverage for those captured fields.

Local .NET 8.0.422 restore/build/smoke verification passed. GitHub Actions is green by user report.

No gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0031A Summary

CodeUpdate-0031A is a corrective build-fix patch for CodeUpdate-0031. GitHub Actions reported a C# syntax error in `Tests/Source/Reforged.Core.Tests/Program.cs` at the configuration smoke-test JSON line. This patch fixes the test JSON string escaping so the test project can compile.

No gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## Current Source of Truth

The GitHub repository is the authoritative source of truth:

`ShisenShi1017/MajorMud-Reforged-V1.0`

Rules:

- Always inspect GitHub truth before planning or changing work.
- Do not rely on old ZIP files, previous patches, or conversation memory when they conflict with GitHub.
- Do not recreate the project from scratch.
- Do not assume old folder layouts still exist.
- Do not claim an update, executable, package, patch, feature, database conversion, build result, or GitHub Actions result exists unless it has actually been verified.

## Work Mode Rules

### Codex mode

Codex is used when working in a local repository coding environment.

- Codex updates the local repository only.
- Codex must wait after each update until the user pushes and states the current push is green in GitHub Actions.
- Codex must not continue to the next Code Update until that green status is confirmed or the user explicitly overrides the gate.
- Codex must update continuity documents before ending the update.

### ChatGPT mode

ChatGPT is used when working in a normal chat window without direct local repository access.

- ChatGPT does not directly modify the user's local repository.
- ChatGPT continues through ZIP upload / ZIP patch workflow unless the user explicitly chooses direct GitHub edits.
- ChatGPT-created update ZIPs must be patch-only, contain exactly one top-level folder, include an apply BAT, include a manifest, and be verified before delivery.

## Never Guess Rule

Never guess when creating, fixing, reviewing, or reporting an update.

- Verify method names before changing code.
- Verify file paths before referencing them.
- Verify package contents before linking ZIPs.
- Verify current update, milestone, and database version from repository files.
- Verify build/test status before claiming success.
- If something cannot be verified, state that clearly instead of filling the gap with assumptions.

## Project Vision

Reforged is a standalone game engine and toolset inspired by the original legacy MUD.

The project goal is to build a standalone Reforged server, shared Core framework, native importer, live editor, launcher, Windows client, Android client, gameplay systems, and plugin framework.

Reforged must not depend on Microsoft Access, OLEDB, the original legacy executable, or the original runtime files during normal server operation. Original legacy files are import/reference material only.

## Required Repository Layout

The current repository layout is frozen unless the user explicitly approves a future restructure.

```text
Apps/
    Launcher/
    Server/
    Editor/
    Importer/
    Client/
    Source/
Core/
Data/
ImportedDatabases/
Docs/
Reports/
Saves/
Tools/
Updates/
Backups/
Bin/
    Windows/
        Launcher/
        Server/
        Editor/
        Importer/
        Client/
    Android/
        Client/
```

## Data Philosophy

`ImportedDatabases/` contains original/reference source data.

`Data/` is the real Reforged game database.

The standalone server must load from `Data/` only. After successful conversion, imported databases and original runtime files are kept only for audit, verification, troubleshooting, and future re-import work.

All WCC data, including WCC default installs, WCC-labeled WBB material, WSGSERVE-related files, and WCC-derived archives, must be treated as reference-only unless the user explicitly states otherwise for a reviewed update.

Eventual cleanup goal: `ImportedDatabases/` should be removable from normal project work only after all needed source data has been decoded, imported, recreated as Reforged-native `Data/`, verified, and explicitly approved for retirement.

OmniVerse may be used as a read-only comparison source for future audits. Website/API data can support a proposed correction, but corrections must be reviewed and approved before changing `Data/`.

## Server Mode Direction

The first real Server target is the Classic baseline. The Server should load and run Classic data first.

Reforged mode, Custom mode, AI systems, alternate gameplay rules, and advanced behavior are not approved yet. Server architecture should stay mode-ready, but only simple extension points needed for future mode/profile loading should be added before the Classic baseline is working.

`Data/` defines world/content. Server defines and enforces engine behavior. The Editor should eventually edit data safely.

Future intended evolution:

1. Classic server baseline.
2. Mode/profile loader.
3. Reforged mode as Classic-derived data/rules overrides.
4. Custom/editor-created worlds.
5. AI systems and advanced behavior only after explicit approval.

## Engine Architecture Direction

Classic is the first target, but the Server is an engine, not a Classic-only server.

Favor reusable engine systems over feature-specific systems. Keep the engine generic whenever practical. Avoid naming or designing systems around Classic unless the behavior is truly Classic-specific.

When designing new Server features, ask whether the feature can be data-driven, reused by future modes, exposed to the Editor later, and implemented without unnecessarily tying the engine to Classic.

Classic, Reforged, custom worlds, future expansions, and additional rule sets should not require separate server codebases. Favor extensibility over shortcuts, but avoid premature complexity.

## WCC Default Install Reference

The user-provided default WCC install archive is preserved under `ImportedDatabases/WCCDefaultInstall/` as reference/import material.

This source is not live `Data/`, not a runtime dependency, and not an approval to rewrite canonical Reforged data. Any conversion or reconciliation must be a separate reviewed update.

## Code Update Rules

Code Updates are for repository, Core, server, editor, importer, launcher, client, tooling, and documentation changes.

Every Code Update must inspect GitHub truth first, verify current state, make the smallest safe approved change, update continuity documents, preserve `Saves/` and `ImportedDatabases/`, and record what was verified.

For ChatGPT ZIP updates, every package must be patch-only, include an `Apply-CodeUpdate-XXXX.bat`, include `UpdateManifest.json`, run from its own extracted folder, require no manual path entry, and be verified before announcement.

## Executable Policy

Do not claim an executable exists unless it has actually been built.

Future executable locations:

```text
Bin/Windows/Launcher/
Bin/Windows/Server/
Bin/Windows/Editor/
Bin/Windows/Importer/
Bin/Windows/Client/
Bin/Android/Client/
```

The user does not want to install SDKs or developer runtimes just to run finished tools. Long term, releases should include runnable builds rather than requiring local developer tool installation.

## Milestone 1 - Core Framework

Status: complete/frozen after the user reported green GitHub Actions for the Milestone 1 freeze sequence.

Milestone 1 established shared models, validation types, repository contracts, configuration contracts, logging contracts, serialization contracts, event system contracts, version metadata, repository health reporting, dependency injection/service registry, smoke-test foundation, and application references to Core.

## Milestone 2 - Engine Modernization / Host & World Editor Planning

Status: active.

CodeUpdate-0029 started Milestone 2 implementation with startup-only Server diagnostics. CodeUpdate-0030 recorded permanent handoff and source-of-truth rules. CodeUpdate-0031 added the first non-gameplay Server bootstrap foundation around the existing diagnostics path. CodeUpdate-0031A corrects the 0031 smoke-test compile failure. CodeUpdate-0032 adds manifest identity output. CodeUpdate-0033 adds an explicit bootstrap exit-code contract. CodeUpdate-0034 adds the OmniVerse read-only reference audit policy. CodeUpdate-0035 adds bootstrap validation summary counts. CodeUpdate-0036 adds knowledge-base/design-decision documentation. CodeUpdate-0037 adds draft Software Design Specification planning records and repairs stale continuity. CodeUpdate-0038 adds required-directory status reporting to Server bootstrap. CodeUpdate-0039 adds host-tool readiness summary output. CodeUpdate-0040 preserves WCC-labeled WBB material as reference/import material only. CodeUpdate-0041 adds the read-only Classic parity audit foundation. CodeUpdate-0042 adds canonical Server data catalog loading and startup reporting. CodeUpdate-0043 adds runtime-readiness classification for canonical data categories. CodeUpdate-0044 adds the original reference capture/extractor tool for reviewed extraction evidence. CodeUpdate-0045 integrates the DataDecode0043 pre-mirror import-readiness checkpoint. CodeUpdate-0046 imports the current OmniVerse/MudInfo reference capture payload. CodeUpdate-0047 preserves DataDecode0049 and records the DataDecode0050 GitHub repository transition. CodeUpdate-0048 adds unified decoded-data comparison reports and Android ChatGPT handoff workflow. CodeUpdate-0049 preserves DataDecode0068 Spell Engine Proof. CodeUpdate-0050 preserves SimulatorBuild0002, RuntimePrep0001-0009 handoff evidence, and DataDecode0110 static certification. CodeUpdate-0051 adds import staging reports and preserves Build0021 behavioral recovery evidence without formula promotion. CodeUpdate-0052 preserves Build0027 current-state handoff evidence. CodeUpdate-0053 replaces repository `Data/` from the embedded Patch0068 `/Data` payload and preserves Patch0068/Handoff0074 evidence under `DataDecodeCheckpoints/`.

Next safe work after CodeUpdate-0053:

1. Wait for user push and green GitHub Actions.
2. Verify and reconcile the new Patch0068-derived `Data/` baseline before further server behavior work.
3. Use Handoff0074 requirements to build a master verification surface from current `Data/` and preserved evidence.
4. Resume executable harness work only after the data baseline is verified enough for that scope.
5. Do not add gameplay systems without explicit user approval.

## Long-Term Roadmap

1. Maintain the frozen Core Framework baseline.
2. Continue engine modernization and host/server startup work.
3. Build native Importer.
4. Build standalone Server runtime from `Data/`.
5. Build server data loading and validation.
6. Build live Editor through server/admin API.
7. Build Launcher and update manager.
8. Build companion client-style Windows Client.
9. Build Android Client.
10. Complete gameplay systems.
11. Add plugin framework.
12. Prepare Version 1.0.

## Important Reminder

This is a professional software project with versioned Code Updates and Database Versions. Accuracy matters more than speed. Never overstate what has been implemented.
