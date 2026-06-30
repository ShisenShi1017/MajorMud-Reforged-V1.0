# AI Context for Reforged

## READ THIS FIRST

This is the authoritative handoff document for every ChatGPT window, Codex session, and development continuation. Read this file before modifying the repository.

Reforged is a standalone game engine and toolset inspired by the original legacy MUD. Original legacy files are import/reference material only. After import, `Data/` is the live editable game database and `ImportedDatabases/` is archive/reference only.

## Required Session Startup Order

1. Read `AGENTS.md`.
2. Read `AI_CONTEXT.md` completely.
3. Read `Docs/Architecture/PROJECT_ROADMAP.md`.
4. Read `README.md`.
5. Read `VERSION.md`.
6. Read `CHANGELOG.md`.
7. Read `ProjectManifest.json`.
8. Read `DEVELOPMENT_PRINCIPLES.md` for engineering decisions.
9. Inspect relevant repository files before making changes.
10. Continue from the latest verified Code Update recorded here.

## Current Repository Facts

| Field | Value |
|---|---|
| Project | Reforged |
| GitHub Truth | `ShisenShi1017/MajorMud-Reforged-V1.0` |
| Current Milestone | Milestone 2 - Engine Modernization / Host & World Editor Planning |
| Current Code Update | 0044 |
| Current Database Version | 0009 |
| Last Update Name | CodeUpdate-0044-Original-Reference-Extractor |
| Milestone Estimate | Milestone 1 frozen; Milestone 2 implementation continues in small verified host/server slices |
| Source of Truth | Current GitHub repository state |
| Runtime Policy | No .NET SDK required for normal runtime |
| ChatGPT Packaging Policy | Patch-only ZIP with exactly one top-level update folder, included Apply-CodeUpdate BAT, and UpdateManifest.json |
| Codex Policy | Codex updates local only and waits for user push plus stated green GitHub Actions before continuing |

## Current Development Goal

Milestone 1 is frozen. Milestone 2 implementation started in CodeUpdate-0029 with constrained host/server startup diagnostics. CodeUpdate-0031 added a first non-gameplay Server bootstrap entry point around the existing diagnostics path. CodeUpdate-0031A corrected the 0031 smoke-test compile failure and is green by user report. CodeUpdate-0032 improves Server bootstrap manifest identity output and is green by user report. CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract and is green by user report. CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits and is green by user report. CodeUpdate-0035 adds Server bootstrap validation summary counts and local .NET 8.0.422 restore/build/smoke verification passed. CodeUpdate-0036 adds the knowledge-base/design-decision documentation baseline through direct GitHub commits. CodeUpdate-0037 combines the 0036B continuity cleanup package with Software Design Specification draft docs and is green by user report. CodeUpdate-0038 adds required-directory status reporting to the non-gameplay Server bootstrap path and is green by user report. CodeUpdate-0039 adds a host-tool readiness summary while keeping gameplay status disabled. CodeUpdate-0040 preserves WCC-labeled WBB material as reference/import material only. CodeUpdate-0041 adds the read-only Classic parity audit foundation and is green by user report. CodeUpdate-0042 adds canonical Server data catalog loading to bootstrap and is green by user report. CodeUpdate-0043 classifies canonical Server data readiness and is green by user report. CodeUpdate-0044 adds the original reference capture/extractor tool.

Do not add gameplay systems without explicit user approval.

## Classic-First Server Direction

The first real Server target is the Classic baseline. The Server should load and run Classic data first.

Do not implement Reforged mode, Custom mode, AI systems, alternate gameplay rules, or advanced behavior until explicitly approved. Avoid hardcoding Classic in a way that blocks future modes; add only simple extension points needed to support future mode/profile loading later.

Responsibility split:

- `Data/` defines world/content.
- Server defines and enforces engine behavior.
- Editor should eventually edit data safely.

Future intended evolution:

1. Classic server baseline.
2. Mode/profile loader.
3. Reforged mode as Classic-derived data/rules overrides.
4. Custom/editor-created worlds.
5. AI systems and advanced behavior after explicit approval.

## Engine-First Architecture Direction

Classic is the first target, but the Server is an engine, not a Classic-only server.

Favor reusable engine systems over feature-specific systems. Avoid naming or designing systems around Classic unless they truly are Classic-specific. World behavior should come from data whenever appropriate, and engine behavior should come from reusable systems rather than hardcoded game logic.

When designing new Server features, ask:

1. Can this be data-driven?
2. Can this be reused by future modes?
3. Can this be exposed to the Editor later?
4. Does this unnecessarily tie the engine to Classic?

Future gameplay systems should plug into the engine instead of replacing it. Classic, Reforged, custom worlds, future expansions, and additional rule sets should not require separate server codebases. Favor extensibility over shortcuts, but avoid premature complexity.

## Imported Reference Sources

- WCC default install archive imported to `ImportedDatabases/WCCDefaultInstall/` during CodeUpdate-0039.
- The preserved source archive is `ImportedDatabases/WCCDefaultInstall/SourceArchive/wcc default.rar`.
- The extracted folder is `ImportedDatabases/WCCDefaultInstall/wcc default/`.
- Archive integrity test passed with 152 extracted files and SHA-256 `92FA1CBD86E00154789950FB2BE443E0CB24C735B631BC44DE14531F0D422E5D`.
- This is reference/import material only. No `Data/` rewrite or runtime dependency is approved.
- WCC-labeled WBB archive material is preserved under `ImportedDatabases/WCCLabeledWBB/` from CodeUpdate-0040 as reference/import material only.
- CodeUpdate-0044 adds `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1` for reference-only capture of original/source material. Capture outputs are evidence only until a separate reviewed reconciliation update changes canonical `Data/`.
- All WCC data, including WCC default installs, WCC-labeled WBB material, WSGSERVE-related files, and WCC-derived archives, is reference-only unless the user explicitly approves a reviewed import/reconciliation update.
- Long-term cleanup goal: retire `ImportedDatabases/` from normal project work only after all needed source data has been decoded, imported, recreated as Reforged-native `Data/`, verified by reports, and explicitly approved for cleanup.

## Permanent Project Rules

- GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the source of truth.
- Before starting a Code Update, inspect GitHub truth and verify current milestone/update/database state.
- Never guess when creating, fixing, reviewing, or reporting updates.
- If something cannot be verified, state that clearly.
- Do not claim code, builds, packages, executables, or GitHub Actions results exist unless verified.
- The repository layout is frozen unless the user explicitly approves a structural change.
- `Data/` is the authoritative game database after import.
- `ImportedDatabases/` is archive/reference only.
- All WCC data is reference-only unless the user explicitly approves otherwise for a reviewed update.
- Do not delete imported/reference material until Reforged-native `Data/` is complete and verified and the user explicitly approves cleanup.
- OmniVerse may be used only as a read-only comparison source. It can suggest possible corrections, but cannot overwrite Reforged `Data/` or `ImportedDatabases/` without reviewed approval.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Runtime must never require the .NET SDK. Development may use SDK tooling, but normal use must launch compiled/runtime-ready artifacts from `Bin/`.
- Every update must update all relevant continuity documents.
- All approved suggestions become implementation requirements unless the user explicitly defers or rejects them.

## Chat Window Mode Rules

### Codex mode

- Codex updates the local repository only.
- Codex must not continue to the next update until the user pushes the current update and states GitHub Actions is green.
- Codex must document local verification and any unverified GitHub gate.

### ChatGPT mode

- ChatGPT cannot directly update the user's local repository.
- ChatGPT uses ZIP upload / ZIP patch workflow for local application unless the user explicitly asks for direct GitHub edits.
- ChatGPT update ZIPs must be patch-only, have exactly one top-level folder, include `Apply-CodeUpdate-XXXX.bat`, include `UpdateManifest.json`, and be physically verified before delivery.

## Milestone 1 Deliverable Status

| Deliverable | Status | Notes |
|---|---:|---|
| Core configuration system | Frozen | Smoke tests verify defaults and flattened JSON loading. |
| Core logging system | Frozen | Logger contract and in-memory logger build; 0024B repaired startup diagnostics logging call. |
| Validation framework | Frozen | Data JSON, cross-reference, and startup diagnostics validation build and smoke-test. |
| Serialization framework | Frozen | JSON data loading foundations build and repository health smoke test passes. |
| Repository interfaces | Frozen | Repository-backed loaders and health checks build. |
| Shared data models | Frozen | Shared data record foundations reduce app-specific model drift. |
| Event system | Frozen | Smoke test verifies publish and subscription disposal behavior. |
| Dependency injection/service registry | Frozen | Smoke test resolves registered services. |
| Version management | Frozen | Keep `VERSION.md` and `ProjectManifest.json` synchronized every update. |
| Project manifest reader | Frozen | Shared manifest model/reader and manifest policy fields exist. |
| Repository health checker | Frozen | Health models and checks exist. |
| Shared utility library | Frozen | Add utilities only when needed by real code. |
| Unit-test framework | Frozen | `Reforged.Core.Tests` runs smoke tests. |
| Applications using Core | Frozen | Server, Importer, Editor, Launcher, and Client source projects reference Core. |

## Milestone 2 Status

| Area | Status | Notes |
|---|---:|---|
| Host/server startup diagnostics | Started | CodeUpdate-0029 added startup-only Server diagnostics. |
| Server bootstrap foundation | Extended in 0043 | 0031 added the bootstrap; 0031A fixed the smoke-test compile error; 0032 captures manifest identity fields; 0033 exposes explicit exit codes; 0035 exposes validation issue counts; 0038 exposes required-directory status; 0039 exposes host-tool readiness; 0041 attaches read-only Classic parity inventory; 0042 loads canonical Server data catalog readiness; 0043 classifies runtime-ready versus runtime-blocked categories. |
| Original reference capture | Started in 0044 | 0044 adds a reference-only capture/extractor tool for original/source archives, files, directories, hashes, category hints, and printable strings. Structured proprietary record decoding remains future reviewed parser work. |
| Engineering principles | Added in 0031 | `DEVELOPMENT_PRINCIPLES.md` records long-term engineering discipline. |
| Knowledge base and SDS planning | Started in 0036/0037 | 0036 added knowledge-base/design-decision records; 0037 adds draft SDS planning docs and repairs stale continuity. |
| Engine modernization | Planning/early implementation | Continue in small verified slices. |
| Host/world editor | Planned | Do not begin larger editor work without approval. |
| Data reference audits | Planned | CodeUpdate-0034 records OmniVerse as a read-only comparison source; no data rewrite. |
| Gameplay systems | Not started | Requires explicit user approval. |
| Networking expansion | Not started | Requires explicit user approval. |

## Update History Summary

| Code Update | Summary |
|---|---|
| 0044 | Adds the original reference capture/extractor tool for reviewed extraction evidence while keeping canonical Data unchanged. |
| 0043 | Classifies canonical Server data as runtime-ready or runtime-blocked while keeping gameplay disabled. |
| 0042 | Adds read-only canonical Server data catalog loading and startup reporting. |
| 0041 | Adds read-only Classic parity audit inventory to Server bootstrap while making no exact parity claim. |
| 0040 | Preserves WCC-labeled WBB material under ImportedDatabases as reference/import material only. |
| 0039 | Adds a compact Server bootstrap readiness summary for host/editor tooling while keeping gameplay disabled. |
| 0038 | Adds required-directory status records/counts to Server bootstrap report and startup output. |
| 0037 | Combines 0036B continuity cleanup with draft Software Design Specification planning documents under `Docs/SDS/`. |
| 0036 | Adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits. |
| 0035 | Adds Server bootstrap validation issue-count summaries for startup output and future host tooling. |
| 0034 | Records OmniVerse as a read-only reference source for future data audits and forbids automatic website-to-Data rewrites. |
| 0033 | Adds an explicit Server bootstrap report exit-code contract and smoke coverage for success/failure exit codes. |
| 0032 | Captures ProjectManifest identity fields in Server bootstrap report/startup output and adds smoke coverage for those fields. |
| 0031A | Corrected invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs` after CodeUpdate-0031 failed GitHub Actions at the test build step. |
| 0031 | Added non-gameplay Server bootstrap entry point/report, bootstrap smoke coverage, `DEVELOPMENT_PRINCIPLES.md`, and refreshed continuity docs. |
| 0030 | Added `AGENTS.md`, corrected source-of-truth and mode rules, removed stale continuity direction, and recorded the never-guess creation rule. |
| 0029 | Added startup-only Server diagnostics and a smoke test for the first constrained Milestone 2 implementation slice. |
| 0028 | Recorded user-reported green CodeUpdate-0027 GitHub Actions and created the first Milestone 2 scope plan. |
| 0027 | Froze the Milestone 1 Core Framework baseline after user-reported green CodeUpdate-0026B GitHub Actions and prepared the Milestone 2 planning handoff. |
| 0026B | Cleaned generated artifacts, repaired ignore rules, and fixed visible documentation encoding damage before baseline freeze. |
| 0026 | Added Milestone 1 verification report, refreshed continuity docs, and prepared final freeze pending GitHub Actions. |
| 0025 | Completed active rebrand audit, removed obsolete duplicate MajorMud-named importer utility, and confirmed no gameplay changes. |
| 0024B | Corrected CoreStartupDiagnostics to use the verified Write(LogSeverity.Information, ...) logger API. |
| 0024 | Added Core verification diagnostics, event bus work, and smoke-test project. |

## CodeUpdate-0044 Handoff

CodeUpdate-0044 is a small non-gameplay Milestone 2 reference-capture update. It adds a safe local extractor/capture utility for original Worldgroup, WCC, WBB, WSGSERVE, and related source material so ChatGPT-assisted extraction work can start from evidence reports instead of guesses.

Implemented:

- Added `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1`.
- Added `Tools/ReferenceCapture/README.md`.
- Preserves source archives under named `ImportedDatabases/ReferenceCaptures/` output folders.
- Extracts ZIP files directly and RAR/7z files when 7-Zip is available.
- Inventories files, SHA-256 hashes, sizes, extensions, category hints, and printable strings.
- Writes JSON, CSV, Markdown, and per-file string reports under named `Reports/ReferenceCaptures/` output folders.
- Recorded CodeUpdate-0043 GitHub Actions as green by user report.
- Recorded future extraction staging as capture, decode, compare, then reviewed apply.

Boundaries:

- No `Data/` rewrite.
- No imported data deletion.
- No original executable execution.
- No keygen use.
- No license bypass.
- No WCC/WBB/WSGSERVE automatic reconciliation.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No exact MajorMUD parity claim.
- Codex did not push to GitHub.

Next-scope candidate:

- Push CodeUpdate-0044 and wait for green GitHub Actions. Then use extractor outputs with ChatGPT for actual extraction analysis and proposed mappings, or approve one narrow server data-readiness/parser/comparison follow-up.

## CodeUpdate-0043 Handoff

CodeUpdate-0043 is a small non-gameplay Milestone 2 server data-readiness update. It separates canonical data file presence from runtime readiness so the Server can report data gaps without failing startup or starting gameplay.

Implemented:

- Added runtime-ready and runtime-blocked counts to `ServerDataCatalogAuditReport`.
- Added placeholder classification to `ServerDataCategorySummary`.
- Added readiness notes for each canonical data category.
- Updated Server startup output to print runtime readiness counts and category notes.
- Added smoke coverage for placeholder categories and runtime-readiness classification.
- Recorded CodeUpdate-0042 GitHub Actions as green by user report.
- Recorded the long-term goal to retire `ImportedDatabases/` only after Reforged-native `Data/` is complete, verified, and explicitly approved for cleanup.

Current local bootstrap result:

- 13/13 registered categories present.
- 8 runtime-ready categories.
- 5 runtime-blocked placeholder categories: loot tables, quests, rooms, shops, and trainers.
- 4,527 records loaded.
- Gameplay remains disabled.

Boundaries:

- No `Data/` rewrite.
- No imported data deletion.
- No WCC import or reconciliation.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No proprietary WCC/WBB/WSGSERVE parser.
- No exact MajorMUD parity claim.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0043 is green by user report and has been superseded by the CodeUpdate-0044 reference-capture handoff above.

## CodeUpdate-0042 Handoff

CodeUpdate-0042 is a small non-gameplay Milestone 2 server-building update. It wires existing Core canonical data loading into Server bootstrap so startup can report which live `Data/` categories are readable before gameplay systems exist.

Implemented:

- Added `ServerDataCatalogAudit` and report models.
- Loaded registered Core data categories through `CoreDataRepositoryLoader`.
- Attached Server data catalog readiness to non-gameplay Server bootstrap.
- Updated Server startup output to print category status, file size, and loaded record count.
- Added smoke coverage for direct Server data catalog loading and bootstrap attachment.
- Recorded CodeUpdate-0041 GitHub Actions as green by user report.

Current local bootstrap result:

- 13/13 registered categories present.
- 4,527 records loaded.
- 5 empty placeholder categories remain visible: loot tables, quests, rooms, shops, and trainers.
- Gameplay remains disabled.

Boundaries:

- No `Data/` rewrite.
- No WCC import or reconciliation.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No proprietary WCC/WBB/WSGSERVE parser.
- No exact MajorMUD parity claim.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0042 is green by user report. Follow the current CodeUpdate-0043 handoff above for next work.

## CodeUpdate-0041 Handoff

CodeUpdate-0041 is a small non-gameplay Milestone 2 server/data-readiness update. It adds a read-only Classic parity audit foundation so Server bootstrap can inventory canonical `Data/` and imported reference material before any field-level parser or gameplay system is introduced.

Implemented:

- Added `ClassicParityAudit` and audit report models.
- Added broad category summaries for rooms, monsters, items, spells, shops, quests, classes/races, ganghouses, maps/paths, and raw legacy databases.
- Attached Classic parity inventory to non-gameplay Server bootstrap.
- Updated Server startup output to print parity inventory status.
- Added smoke coverage for audit inventory and bootstrap attachment.
- Recorded the permanent rule that all WCC data is reference-only unless explicitly approved otherwise.

Boundaries:

- No `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No proprietary WCC/WBB/WSGSERVE parser yet.
- No exact MajorMUD parity claim.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0041 is green by user report. Follow the current CodeUpdate-0042 handoff above for next work.

## CodeUpdate-0039 Handoff

CodeUpdate-0039 is a small non-gameplay Milestone 2 host/server update. It adds a compact readiness summary to Server bootstrap so future host/editor tooling can inspect startup readiness without parsing individual diagnostics.

Implemented:

- Added `ServerBootstrapReadiness` to `ServerBootstrapReport`.
- Added readiness flags for project root, required directories, manifest identity, startup diagnostics, and host-tool readiness.
- Added startup output for readiness status.
- Kept gameplay status explicitly disabled.
- Added smoke-test coverage for readiness status.
- Folded in the Classic-first server direction as planning/continuity documentation.
- Folded in the engine-first architecture direction as planning/continuity documentation.
- Added the user-provided WCC default install archive under `ImportedDatabases/WCCDefaultInstall/` as reference/import material.
- Recorded CodeUpdate-0038 GitHub Actions as green by user report.

Boundaries:

- No `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No Reforged mode, Custom mode, AI systems, alternate gameplay rules, or mode/profile loader implementation.
- No `Data/` rewrite from the WCC default install.
- No use of WCC executable/runtime files as live server dependencies.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0039 was followed by reference-import and parity-audit work. Current next work should follow the CodeUpdate-0041 handoff above.

## CodeUpdate-0038 Handoff

CodeUpdate-0038 is a small non-gameplay Milestone 2 host/server update. It makes the required directory verification already performed by Server bootstrap visible as structured report data and startup output.

Implemented:

- Added `RequiredDirectoryStatus` records to `ServerBootstrapReport`.
- Added total, present, and missing required-directory counts.
- Updated `ServerBootstrap` to record every required directory and its resolved path.
- Updated Server startup output to print required-directory summary and details.
- Added smoke-test coverage for required-directory status.
- Recorded CodeUpdate-0037 GitHub Actions as green by user report.

Boundaries:

- No `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0038 GitHub Actions is green by user report. The next slice is CodeUpdate-0039 bootstrap readiness summary.

## CodeUpdate-0037 Handoff

CodeUpdate-0037 is a documentation-only update combining the user-provided CodeUpdate-0036B continuity cleanup ZIP and the user-provided CodeUpdate-0037 Software Design Specification ZIP.

Implemented:

- Fast-forwarded local `main` to GitHub `origin/main` commit `105ed76b`, which contains CodeUpdate-0036.
- Synchronized stale continuity documents from CodeUpdate-0035/0036 to CodeUpdate-0037.
- Recorded CodeUpdate-0036 as the direct-GitHub knowledge-base/design-decision update.
- Added `Docs/SDS/` draft planning documents for future systems.
- Added CodeUpdate-0037 documentation and verification report.

Boundaries:

- No source-code behavior changes.
- No `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No raw third-party archive import.
- Codex did not push to GitHub.

Next-scope candidate:

- Push CodeUpdate-0037 and wait for green GitHub Actions. After that, approve one small Milestone 2 host/server or planning slice.

## CodeUpdate-0035 Handoff

CodeUpdate-0035 returns to the pre-website Milestone 2 host/server path. It adds validation issue counts to the Server bootstrap report so startup output and future host tooling can summarize validation state without parsing raw issue text.

Implemented:

- Added total, info, warning, error, and fatal issue-count properties to `ServerBootstrapReport`.
- Updated Server startup output to print a validation summary by severity.
- Added smoke-test coverage for validation issue counts on successful and failed bootstrap paths.
- Recorded CodeUpdate-0034 as GitHub Actions green by user report.

Boundaries:

- No `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No BAT installer or ZIP package was created by Codex.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0036 should be another small host/server slice approved by the user before implementation.

## CodeUpdate-0034 Handoff

CodeUpdate-0034 is a documentation/reference-audit update. The user clarified that OmniVerse should not rewrite Reforged data unless it helps identify a reviewed correction. This update records OmniVerse as a comparison source only.

Implemented:

- Added `Docs/DataSources/OmniVerse-Reference-Audit.md`.
- Added `Docs/CodeUpdates/CodeUpdate-0034-OmniVerse-Reference-Audit-Plan.md`.
- Added `Reports/CodeUpdates/CodeUpdate-0034/OmniVerseReferenceAuditPlanReport.md`.
- Recorded verified OmniVerse public endpoints for MajorMUD `wcc` reference discovery.
- Recorded CodeUpdate-0033 as GitHub Actions green by user report.
- The user reported CodeUpdate-0034 GitHub Actions green after push.

Reference policy:

- Reforged `Data/` remains canonical.
- `ImportedDatabases/` remains archive/reference only.
- OmniVerse can identify likely mismatches, but cannot overwrite data.
- Any correction requires source field, Reforged field, difference, confidence, and user approval in a follow-up update.

Boundaries:

- No `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No BAT installer or ZIP package was created by Codex.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0035 should be approved by the user before implementation. A safe candidate is a read-only local audit report template or offline comparison script that does not modify `Data/`.

## CodeUpdate-0033 Handoff

CodeUpdate-0033 is a small Milestone 2 host/server bootstrap improvement. It makes bootstrap process-result behavior explicit by exposing an `ExitCode` value on `ServerBootstrapReport`, then returns that value from Server startup.

Implemented:

- Added `ServerBootstrapReport.ExitCode`.
- Updated Server startup to return `report.ExitCode` for both success and failure.
- Added smoke-test coverage for success exit code 0 and failure exit code 1.
- Recorded CodeUpdate-0032 as GitHub Actions green by user report.
- The user reported CodeUpdate-0033 GitHub Actions green after push.

Boundaries:

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No BAT installer or ZIP package was created by Codex.
- Codex did not push to GitHub.

Next-scope candidate:

- CodeUpdate-0034 should be another small host/server slice approved by the user before implementation.

## CodeUpdate-0032 Handoff

CodeUpdate-0032 is a small Milestone 2 host/server bootstrap improvement. It carries ProjectManifest identity values into the Server bootstrap report and console output so future startup diagnostics and handoffs can name the active project, CodeUpdate, database version, and milestone without relying on scattered document reading.

Implemented:

- Added project, CodeUpdate, database version, and milestone fields to `ServerBootstrapReport`.
- Updated `ServerBootstrap` to capture those fields while validating `ProjectManifest.json`.
- Updated Server startup output to print the captured identity values.
- Added smoke-test coverage for the captured identity fields.
- Added a handoff improvement rule: future updates should record a concise next-scope candidate and explicit boundaries separately from completed work.
- The user reported CodeUpdate-0032 GitHub Actions green after push.

Boundaries:

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No BAT installer or ZIP package was created by Codex.
- Codex did not push to GitHub.

## CodeUpdate-0031A Handoff

CodeUpdate-0031A is a corrective build-fix patch for CodeUpdate-0031. The user reported GitHub Actions failed with repeated `Syntax error, ',' expected` messages at `Tests/Source/Reforged.Core.Tests/Program.cs#L59`. GitHub truth confirmed the configuration smoke test used an unescaped JSON string inside a normal C# string literal.

Implemented:

- Fixed `Tests/Source/Reforged.Core.Tests/Program.cs` by escaping the JSON test string.
- Updated continuity documents and report files.
- Synchronized `UpdateManifest.json` to the verified CodeUpdate-0031A baseline after a stale 0029 manifest was found locally.

Boundaries:

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No live editor implementation.

## Verification Status

Verified before creating CodeUpdate-0037:

- GitHub `origin/main` was fetched.
- Local `main` was behind GitHub by six CodeUpdate-0036 commits.
- Local `main` was fast-forwarded to `origin/main` commit `105ed76b`.
- CodeUpdate-0036B and CodeUpdate-0037 ZIP contents were inspected before merging.
- JSON parse and local .NET 8.0.422 restore/build/smoke verification passed for CodeUpdate-0037.
- Initial parallel Client and Editor build attempts hit a transient shared Core `obj` file lock; both passed when rerun sequentially.
- The user reported CodeUpdate-0037 GitHub Actions green before CodeUpdate-0038 started.

Verified before creating CodeUpdate-0038:

- Local `main` was clean and aligned with `origin/main` at commit `ad40ba85`.
- Repository manifests recorded CodeUpdate-0037 and DatabaseVersion 0009.
- User reported CodeUpdate-0037 GitHub Actions green.

Verified before creating CodeUpdate-0039:

- Local `main` was clean and aligned with `origin/main` at commit `fa0b7f2e`.
- Repository manifests recorded CodeUpdate-0038 and DatabaseVersion 0009.
- User reported CodeUpdate-0038 GitHub Actions green.

CodeUpdate-0044 is the current local update candidate.

CodeUpdate-0043 is green by user report. CodeUpdate-0044 remains pending user push and GitHub Actions green report.

## Next Recommended Work

1. Push CodeUpdate-0044 and report GitHub Actions status.
2. After green Actions, use reference-capture outputs with ChatGPT for reviewed extraction analysis or approve one narrow server data-readiness/Classic parser/comparison scope.
3. Do not add gameplay systems without explicit approval.

## New Window Instruction

If this is a new ChatGPT or Codex session, do not ask the user to restate project history. Read `AGENTS.md`, this file, the roadmap, README, VERSION, CHANGELOG, ProjectManifest, and `DEVELOPMENT_PRINCIPLES.md`, inspect GitHub truth, then continue from the next recommended work unless the user gives different instructions.
