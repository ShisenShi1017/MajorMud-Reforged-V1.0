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
| Current Code Update | 0058 |
| Current Database Version | 0009 |
| Last Update Name | CodeUpdate-0058-World-Projection-Index |
| Milestone Estimate | Milestone 1 frozen; Milestone 2 implementation continues in small verified host/server slices |
| Source of Truth | Current GitHub repository state |
| Runtime Policy | No .NET SDK required for normal runtime |
| ChatGPT Packaging Policy | Patch-only ZIP with exactly one top-level update folder, included Apply-CodeUpdate BAT, and UpdateManifest.json |
| Codex Policy | Codex updates local only and waits for user push plus stated green GitHub Actions before continuing |

## Current Development Goal

Milestone 1 is frozen. Milestone 2 implementation started in CodeUpdate-0029 with constrained host/server startup diagnostics. CodeUpdate-0031 added a first non-gameplay Server bootstrap entry point around the existing diagnostics path. CodeUpdate-0031A corrected the 0031 smoke-test compile failure and is green by user report. CodeUpdate-0032 improves Server bootstrap manifest identity output and is green by user report. CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract and is green by user report. CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits and is green by user report. CodeUpdate-0035 adds Server bootstrap validation summary counts and local .NET 8.0.422 restore/build/smoke verification passed. CodeUpdate-0036 adds the knowledge-base/design-decision documentation baseline through direct GitHub commits. CodeUpdate-0037 combines the 0036B continuity cleanup package with Software Design Specification draft docs and is green by user report. CodeUpdate-0038 adds required-directory status reporting to the non-gameplay Server bootstrap path and is green by user report. CodeUpdate-0039 adds a host-tool readiness summary while keeping gameplay status disabled. CodeUpdate-0040 preserves WCC-labeled WBB material as reference/import material only. CodeUpdate-0041 adds the read-only Classic parity audit foundation and is green by user report. CodeUpdate-0042 adds canonical Server data catalog loading to bootstrap and is green by user report. CodeUpdate-0043 classifies canonical Server data readiness and is green by user report. CodeUpdate-0044 adds the original reference capture/extractor tool. CodeUpdate-0045 integrates the DataDecode0043 checkpoint as the pre-mirror import-readiness baseline and is green by user report. CodeUpdate-0046 imports the OmniVerse/MudInfo capture payload. CodeUpdate-0047 preserves DataDecode0049 and records the DataDecode0050 GitHub transition handoff so future decode passes work directly in the repository. CodeUpdate-0048 creates the unified decoded-data comparison folder and Android ChatGPT phone handoff workflow. CodeUpdate-0049 preserves DataDecode0068 Spell Engine Proof and refreshes unified decoded-data inventory. CodeUpdate-0050 preserves SimulatorBuild0002 handoff evidence, DataDecode0110 final static certification, RuntimePrep0001-0009 archives, and the expected-model simulator. CodeUpdate-0051 adds import staging reports and preserves Build0021 behavioral recovery evidence without promoting formulas or rewriting canonical `Data/`. CodeUpdate-0052 preserves Build0027 current-state handoff evidence. CodeUpdate-0053 replaces repository `Data/` from the embedded Patch0068 `/Data` payload, preserves Patch0068 and Handoff0074 evidence once under `DataDecodeCheckpoints/`, and records Patch0071 as unreadable/unapplied. CodeUpdate-0054 copies only repository-new files from Validation0013 and DocUpdate0024 while skipping all existing paths. CodeUpdate-0055 preserves Validation0043 and MegaMUD2 RE0021 as evidence-only checkpoints, refreshes UnifiedDecodedData after correcting stale placeholder wording, and records the current placeholder/blocker audit. CodeUpdate-0057 repairs continuity after `CoreUpdate 0056` left manifests/docs at 0055, folds in Server Build Session 0001 and DocUpdate0025 planning material, and adds the first non-gameplay modular Server foundation with MajorMUD Classic registered as a disabled data-driven module baseline. CodeUpdate-0058 closes the `world_rooms` projection gap with scalar string-array loading and read-only Server world projection diagnostics.

Do not add gameplay systems without explicit user approval.

## Modular Server Direction

MajorMUD is the first game module, not the whole Server. CodeUpdate-0057 registers `majormud-classic` through a read-only Server game module catalog with gameplay disabled.

The Server should remain a reusable game-hosting platform. MajorMUD-specific commands, formulas, rooms, spells, monsters, shops, and world behaviors should live in module/rule/data layers instead of generic Server host code.

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

Favor reusable engine systems over feature-specific systems. Avoid naming or designing systems around Classic unless they truly are Classic-specific. Game logic is data-driven by default: rules, content, progression, encounters, shops, quests, spawns, textblock actions, and world behavior should live in data unless a behavior is truly engine-internal. The server supplies reusable engine systems, validation, execution, and enforcement rather than hardcoded game-specific logic.

When designing new Server features, ask:

1. Is this implemented through data unless it is truly engine-internal?
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
- CodeUpdate-0045 adds the DataDecode0043 checkpoint under `DataDecodeCheckpoints/DataDecode0043/`. DataDecode0043 is a pre-mirror import-readiness baseline only, with 0 canonical field promotions.
- CodeUpdate-0046 imports the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only. The payload includes WCC OmniVerse API captures for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommap files, 1,246 room detail files, a 23-entry room capture error log, the MudInfo `maps.rar` source archive, and 102 extracted MudInfo map files.
- CodeUpdate-0047 preserves DataDecode0049 under `DataDecodeCheckpoints/DataDecode0049/` and records DataDecode0050 transition continuity under `DataDecodeCheckpoints/DataDecode0050/`. Future decode passes should update the repository directly rather than working from standalone ZIP repositories.
- CodeUpdate-0048 adds `DataDecodeCheckpoints/UnifiedDecodedData/` as a generated inventory/comparison surface and `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md` for phone-based ChatGPT decode handoffs.
- CodeUpdate-0049 adds `DataDecodeCheckpoints/DataDecode0068/` as a repository-preserved Spell Engine Proof checkpoint. DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.
- CodeUpdate-0050 adds `DataDecodeCheckpoints/DataDecode0110/`, `DataDecodeCheckpoints/SimulatorBuild0002/`, and `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`. SimulatorBuild0002 is expected-model evidence only until live runtime validation exists.
- CodeUpdate-0051 adds `DataDecodeCheckpoints/ImportStaging/` as a reviewed import staging surface and preserves Build0021 behavioral recovery evidence under `Docs/BehavioralRecovery/Build0021/`, `DataDecodeCheckpoints/SimulatorBuild0021/`, `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`, and `Core/Runtime/BehavioralRecovery/README.md`. Build0021 is evidence/catalog material only and does not certify live runtime behavior.
- CodeUpdate-0052 adds `DataDecodeCheckpoints/CurrentStateBuild0027/` and `Docs/BehavioralRecovery/Build0027/`. Build0027 is a current-state handoff only. It records that original runtime validation remains unavailable and that Build0024/0025 matrices are `expected_model` until converted into real executable simulator tests.
- CodeUpdate-0053 replaces repository `Data/` from the embedded Patch0068 `/Data` payload. Patch0068 findings are preserved once under `DataDecodeCheckpoints/Patch0068/`, Handoff0074 is preserved once under `DataDecodeCheckpoints/Handoff0074/`, and duplicate `Docs/PatchHandoffs/` copies are intentionally not retained.
- CodeUpdate-0054 copies only repository-new files from Validation0013 and DocUpdate0024. Existing repository paths are skipped and not overwritten.
- CodeUpdate-0055 preserves curated current-pass Validation0043 evidence under `DataDecodeCheckpoints/Validation0043/` and curated current-pass MegaMUD2 RE0021 evidence under `DataDecodeCheckpoints/MegaMUD2RE0021/`. Validation0043 contributes matched textblock action dispatcher/opcode evidence; MegaMUD2 RE0021 contributes route/client/protocol text-command evidence. Neither package changes canonical `Data/`.
- CodeUpdate-0057 folds Server Build Session 0001 into `Docs/ServerBuild/Session0001/` and adds the interactive client map planning placeholder under `Docs/Architecture/InteractiveClientMap.md`. These are planning/handoff materials only and do not change canonical `Data/`.
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
| Server bootstrap foundation | Extended in 0058 | 0031 added the bootstrap; 0031A fixed the smoke-test compile error; 0032 captures manifest identity fields; 0033 exposes explicit exit codes; 0035 exposes validation issue counts; 0038 exposes required-directory status; 0039 exposes host-tool readiness; 0041 attaches read-only Classic parity inventory; 0042 loads canonical Server data catalog readiness; 0043 classifies runtime-ready versus runtime-blocked categories; 0057 attaches a read-only game module catalog; 0058 attaches read-only world projection diagnostics. |
| Original reference capture | Started in 0044 | 0044 adds a reference-only capture/extractor tool for original/source archives, files, directories, hashes, category hints, and printable strings. Structured proprietary record decoding remains future reviewed parser work. |
| Data decode checkpointing | Started in 0045 | 0045 integrates DataDecode0043 as the pre-mirror import-readiness baseline. Full DataDecode0044 should wait for OmniVerse HTTrack mirror indexing or runtime/debug evidence. |
| OmniVerse/MudInfo capture | Imported reference evidence in 0046 | 0046 records HTTrack shell/assets as external context and imports WCC API JSON, shop-seeded roommaps/details, capture error logs, and MudInfo maps under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only. |
| Decode repository transition | Active in 0047 | 0047 preserves DataDecode0049 and records DataDecode0050 transition rules. Future decode passes use GitHub as the direct working copy; ZIPs are archival artifacts only. |
| Unified decoded data | Active in 0048 | 0048 adds generated inventory/comparison reports under `DataDecodeCheckpoints/UnifiedDecodedData/`, comparing canonical/native decode data against the CodeUpdate0046 OmniVerse reference payload. |
| Android ChatGPT workflow | Active in 0048 | 0048 adds phone handoff docs/templates so decode work from Android ChatGPT can be structured without PowerShell. |
| Spell engine proof | Preserved in 0049 | 0049 preserves DataDecode0068, recording spell proof evidence and 0 field promotions. Next proof target is DataDecode0069 executable dataflow. |
| Simulator/runtime-prep evidence | Preserved in 0050/0051 | 0050 preserves SimulatorBuild0002, RuntimePrep0001-0009 archives, DataDecode0110 static certification, and the expected-model simulator. 0051 preserves Build0021 behavioral recovery evidence. Live runtime validation remains pending. |
| Import staging | Active in 0051 | 0051 adds report-only import staging queues and phone handoff templates before any reviewed canonical `Data/` rewrite. |
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
| 0058 | Closes the `world_rooms` projection gap with scalar string-array loading and read-only Server world projection diagnostics. |
| 0057 | Repairs 0056 continuity drift and adds a non-gameplay modular Server foundation with MajorMUD Classic registered as a disabled data-driven module baseline. |
| 0055 | Preserves Validation0043 and MegaMUD2 RE0021 evidence, refreshes UnifiedDecodedData, and records the current placeholder/blocker audit. |
| 0054 | Copies only repository-new files from Validation0013 and DocUpdate0024; skips existing paths and preserves the 0053 `Data/` baseline. |
| 0053 | Replaces repository `Data/` from the embedded Patch0068 `/Data` payload and preserves Patch0068/Handoff0074 evidence once under `DataDecodeCheckpoints/`; Patch0071 is unreadable/unapplied. |
| 0052 | Preserves Build0027 current-state handoff evidence and records Build0028 real executable harness as the next recommended scope. |
| 0051 | Adds report-only import staging readiness reports and preserves Build0021 behavioral recovery evidence without formula promotion. |
| 0050 | Preserves SimulatorBuild0002 handoff evidence, DataDecode0110 static certification, RuntimePrep archives, and expected-model simulator tooling. |
| 0049 | Preserves DataDecode0068 Spell Engine Proof and refreshes unified decoded-data inventory. |
| 0048 | Adds unified decoded-data inventory/comparison reports and Android ChatGPT phone handoff workflow. |
| 0047 | Preserves DataDecode0049 and records the DataDecode0050 GitHub transition so future decode passes commit directly to the repository. |
| 0046 | Imports current OmniVerse/MudInfo reference payload and records partial shop-seeded room evidence. |
| 0045 | Integrates the DataDecode0043 pre-mirror import-readiness checkpoint without changing Data or server behavior. |
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

## CodeUpdate-0055 Handoff

CodeUpdate-0055 is an evidence-preservation and placeholder-audit update.

Implemented:

- Preserved 19 curated current-pass files from `MajorMUD-Reforged-Living-Validation0043-Cumulative.zip` under `DataDecodeCheckpoints/Validation0043/`.
- Recorded Validation0043 ZIP SHA-256 `03A164E1771CAF9B854205D220C0CFE84DCD34D1CE3FA5A80D586C61657E9BC0`.
- Verified the 92 Validation0043 package `Data/` files hash-match the current repository `Data/` files; no canonical `Data/` overlay was applied.
- Preserved 15 curated current-pass files from `ReforgedRealms-MegaMUD2-Living-RE0021-PacketEvidenceExpansion-Real-Cumulative.zip` under `DataDecodeCheckpoints/MegaMUD2RE0021/`.
- Recorded MegaMUD2 RE0021 ZIP SHA-256 `6EEC25347C63D1E6E2ABB58666A65C882D5A6DDA5DA95785A108DDAB6EB4A7BC`.
- Repaired `Tools/DataDecode/Build-UnifiedDecodedData.ps1` so populated rooms, shops, and monsters are no longer described as placeholders.
- Regenerated `DataDecodeCheckpoints/UnifiedDecodedData/`.
- Added `Reports/CodeUpdates/CodeUpdate-0055/CurrentDataPlaceholderAudit.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0055/RuntimeParityBlockerSummary.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0055/PackageIntakeInventory.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0055/Validation0043MegaMUD2RE0021PlaceholderAuditReport.md`.
- Recorded that game logic is data-driven by default: rules, content, progression, encounters, shops, quests, spawns, textblock actions, and world behavior should live in data unless truly engine-internal.

Current placeholder/blocker state:

- `Data/quests.json` is the only true empty canonical placeholder.
- `world_rooms` was a server catalog projection gap from `Data/world.json`; CodeUpdate-0058 resolves it.
- 367 runtime parity blockers remain: 211 room-exit same-map, 149 room linked-spawn, 5 shop inventory, 1 monster loot, and 1 loot table item blocker.

Verification:

- Manifest JSON parse passed.
- Report CSV parse passed.
- `git diff --check` passed.
- Sequential restore/build for Core, Server, Importer, Editor, Launcher, Client, and Core.Tests passed with 0 warnings and 0 errors.
- Core/server smoke tests passed.
- Server bootstrap run passed and reported CodeUpdate 0055, 11 runtime-ready categories, 2 runtime-blocked categories, and 1 placeholder.

Boundaries:

- No canonical `Data/` overlay.
- No imported/reference deletion.
- No gameplay systems.
- No server behavior change.
- No field promotion.
- No raw binary packet layout inference.
- No live runtime validation claim.
- Codex did not push to GitHub.

Next-scope candidate:

- Superseded by CodeUpdate-0058: `world_rooms` projection is resolved. Future work should choose one narrow non-gameplay Server construction slice before adding gameplay behavior.

## CodeUpdate-0054 Handoff

CodeUpdate-0054 is a new-file-only evidence and planning intake update.

Implemented:

- Recorded CodeUpdate-0053 GitHub Actions as green by user report.
- Opened and inventoried `MajorMUD-Reforged-Validation0013-Cumulative.zip`.
- Recorded Validation0013 ZIP SHA-256 `5E8804557B371C526F48FEF2C7333F344A66316FF48ECE7C3E209FD260297AD1`.
- Copied 437 repository-new files from Validation0013.
- Skipped 86 existing repository paths from Validation0013 without overwriting them.
- Opened and inventoried `DocUpdate-0024-Librarium-Calculation-Engine-Placeholder.zip`.
- Recorded DocUpdate0024 ZIP SHA-256 `2022CF91CDF85D5E0136B7765DF9B8EE7D0F193C6E9FCCE4A2B2AF59C8DEED02`.
- Copied 2 repository-new files from DocUpdate0024.
- Added `Reports/CodeUpdates/CodeUpdate-0054/NewFilesCopied.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0054/ExistingFilesSkipped.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0054/Validation0013DocUpdate0024NewFilesIntakeReport.md`.

Verification:

- Copied JSON files parsed successfully: 72.
- Copied CSV files were readable: 183.
- `Tools/MDBExtractor/offline_mdb_text_extractor.py` passed Python bytecode compilation.

Boundaries:

- No existing repository file overwrite.
- No replacement of the CodeUpdate-0053 `Data/` baseline.
- No imported/reference deletion.
- No server behavior change.
- No gameplay systems.
- No Patch0071 application.
- No Patch0074 master verification implementation.
- No live runtime validation claim.
- Codex did not push to GitHub.

Next-scope candidate:

- Push CodeUpdate-0054 and wait for green GitHub Actions. Then build a focused verification index over Validation0013 evidence and the current `Data/` baseline. Promote or overwrite nothing until exact file/field differences are reviewed and approved.

## CodeUpdate-0053 Handoff

CodeUpdate-0053 is a data-baseline and evidence-intake update.

Implemented:

- Recorded CodeUpdate-0052 GitHub Actions as green by user report.
- Replaced repository `Data/` from exactly 86 embedded Patch0068 `/Data` files.
- Preserved Patch0068 findings once under `DataDecodeCheckpoints/Patch0068/`.
- Preserved Handoff0074 once under `DataDecodeCheckpoints/Handoff0074/`.
- Recorded Patch0068 source ZIP SHA-256 `16A8C3932CF23B2B9AB2D3E9DFCA605ABA4EA09FE8BD2E77519AA385CDFD515C`.
- Recorded Patch0068 embedded updated-source ZIP SHA-256 `96DDFC1D1658BA3D98678FA28ECF185E6E6B7A1D5A6C9D725332FC2C88507AC3`.
- Recorded Handoff0074 source ZIP SHA-256 `B6132632A154A43AD32C1AA8CEE681E82D994CCA82A360C7728BBF64228D8AD8`.
- Recorded Patch0071 source ZIP SHA-256 `56B2A080BA89263F08BE134DA8DE547D941AF3B8D0CE5E4D5B9BCF1C5340F881`.
- Removed duplicate `Docs/PatchHandoffs/` evidence copies.
- Updated server data-readiness diagnostics and smoke expectations so populated Patch0068 data categories are no longer treated as empty placeholders.
- Added `Reports/CodeUpdates/CodeUpdate-0053/Handoff0074Patch0071Patch0068IntakeReport.md`.

Verification:

- Handoff0074 copied files were checked against `SHA256SUMS.txt`; 5 of 6 entries matched and the self-hash mismatch was documented.
- Patch0068 copied manifest-listed files were checked against `PATCH_0068_MANIFEST.csv`; 7 of 7 copied entries matched.
- Patch0068 CSV files parsed locally.
- Patch0068 nested updated-source `Data/` surface records 86 rows.
- Repository `Data/` was replaced from exactly 86 staged Patch0068 `/Data` files.
- Patch0071 could not be read by the built-in ZIP reader and was not applied.

Boundaries:

- No non-`Data` Patch0068 overlay.
- No Patch0071 application.
- No Patch0074 master verification implementation.
- No imported/reference deletion.
- No gameplay server behavior change.
- No gameplay systems.
- No live Worldgroup runtime validation claim.
- Codex did not push to GitHub.

Next-scope candidate:

- Push CodeUpdate-0053 and wait for green GitHub Actions. Then build a master verification/reconciliation surface over current `Data/`, Handoff0074 requirements, Patch0068 evidence, and any readable later Patch0071/Patch0073/Patch0074 materials.

## CodeUpdate-0052 Handoff

CodeUpdate-0052 is a documentation/evidence preservation update for the user-provided Build0027 current-state handoff.

Implemented:

- Recorded CodeUpdate-0051 GitHub Actions as green by user report.
- Inspected `MajorMUD-Reforged-CodexHandoff-CurrentState-Build0027.zip`.
- Recorded source ZIP SHA-256 `7932FA3E6D4664D5440C0039CBF7FD85528CB0A5CBC09C7BE3C25CB2E4CF750F`.
- Verified extracted Build0027 files against `MANIFEST.json`.
- Preserved full handoff under `DataDecodeCheckpoints/CurrentStateBuild0027/`.
- Preserved human-readable docs/tasks/evidence under `Docs/BehavioralRecovery/Build0027/`.
- Added `Reports/CodeUpdates/CodeUpdate-0052/Build0027CurrentStateHandoffReport.md`.

Verification:

- JSON and CSV parse validation passed.
- Build0027 checkpoint files matched the handoff manifest hashes.
- Local sequential Core and Core.Tests Release builds passed with 0 warnings and 0 errors.
- Core/server smoke tests passed.
- An earlier parallel build attempt hit a transient shared `obj` file lock.

Build0027 current-state rules:

- Static data/content recovery is effectively complete by handoff statement.
- Entity/world/catalog recovery is effectively complete by handoff statement.
- Simulator framework is present and partially runnable.
- Expected-model matrices are present for Build0024 and Build0025.
- Original runtime validation remains unavailable.
- Build0024 and Build0025 matrices are `expected_model` until converted into actual executable simulator tests.
- Earlier chat pass/fail claims are not authoritative unless backed by artifact logs or locally generated test output.

Boundaries:

- No executable harness implementation.
- No canonical `Data/` rewrite.
- No imported/reference deletion.
- No field promotion.
- No gameplay systems.
- No server behavior change.
- No original runtime behavior certification.
- No formula certification from chat summaries.

Next-scope candidate:

- Superseded by CodeUpdate-0053. Current next work is a master verification/reconciliation surface over the Patch0068-derived `Data/` baseline before resuming executable harness work.

## CodeUpdate-0051 Handoff

CodeUpdate-0051 is a report-only import staging and evidence preservation update.

Implemented:

- Added `Tools/DataDecode/Build-ImportStagingReport.ps1`.
- Generated `DataDecodeCheckpoints/ImportStaging/` with canonical data surface, simulator evidence surface, staging queue, phone handoff template, manifest, README, and report files.
- Preserved Build0021 behavioral recovery evidence under `Docs/BehavioralRecovery/Build0021/`.
- Preserved Build0021 checkpoint material under `DataDecodeCheckpoints/SimulatorBuild0021/`.
- Added `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`.
- Added `Core/Runtime/BehavioralRecovery/README.md`.
- Added `Reports/CodeUpdates/CodeUpdate-0051/ImportStagingAndBuild0021Report.md`.
- Recorded CodeUpdate-0050 GitHub Actions as green by user report.

Verification:

- JSON/CSV/report validation passed for CodeUpdate-0051 files.
- `Tools/DataDecode/Build-ImportStagingReport.ps1` ran successfully.
- Build0021 copied files match the outer handoff manifest hashes.
- The internal Build0021 manifest has documented stale rows for its own manifest and verification JSON.
- Sequential local Core and Core.Tests Release builds passed with 0 warnings and 0 errors.
- Core/server smoke tests passed.

Build0021 findings:

- 6 `verified_from_data` formula/catalog entries.
- 4 `strong_candidate` entries.
- 2 `weak_candidate` entries.
- 4 `runtime_required` entries.
- Runtime used: no.
- Live Worldgroup validation: no.
- Certified simulator executable formula updates: none.

Import staging findings:

- `Data/rooms.json`: 0 canonical records; 1,246 evidence-backed room records.
- `Data/shops.json`: 0 canonical records; 178 evidence-backed shop records.
- `Data/loot_tables.json`: 0 canonical records; 732 monster item drop edges.
- `Data/quests.json`: 0 canonical records; 125 quest/progression candidates.
- `Data/trainers.json`: 0 canonical records; 46 skill/trainer-related rows.
- `Data/items.json`, `Data/spells.json`, and `Data/monsters.json` require reviewed reconciliation before any rewrite.

Boundaries:

- No `Data/` rewrite.
- No imported/reference deletion.
- No field promotion.
- No gameplay systems.
- No server behavior change.
- No live Worldgroup runtime validation claim.
- No OmniVerse-only promotion.
- No public-ID to binary-row assumptions.
- No `strong_candidate` formula certification.
- No `runtime_required` behavior promotion.

Next-scope candidate:

- CodeUpdate-0051 is green by user report and has been superseded by the CodeUpdate-0052 Build0027 current-state handoff.

## CodeUpdate-0050 Handoff

CodeUpdate-0050 is a documentation/reference/tooling preservation update for the user-provided SimulatorBuild0002 handoff package.

Implemented:

- Added `DataDecodeCheckpoints/SimulatorBuild0002/`.
- Added `DataDecodeCheckpoints/SimulatorBuild0002/README.md`.
- Added `DataDecodeCheckpoints/DataDecode0110/`.
- Added selected DataDecode0099-0108 DAT decoding decision docs under `Docs/DATDecoding/`.
- Added `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`.
- Added `Reports/CodeUpdates/CodeUpdate-0050/SimulatorBuild0002HandoffReport.md`.
- Refreshed `DataDecodeCheckpoints/UnifiedDecodedData/`; source inventory now has 1,725 rows.
- Updated `Tools/DataDecode/Build-UnifiedDecodedData.ps1` so generated manifest `codeUpdate` comes from `ProjectManifest.json` instead of a hardcoded value.
- Recorded SimulatorBuild0002 source package SHA-256 `66DF6BF0E1885A96C1B3BFB9A9D535FEDA8FA9D025A807509175B4C644D758DA`.
- Excluded the embedded stale full repository archive and generated Python cache files.

DataDecode0110 metrics:

- Imported database files checked: 3,908.
- Imported database CSV files: 5.
- Imported database MDB files: 1.
- Unsupported inferences made: 0.
- Unresolved ownership rows carried forward: 3,975.
- Unresolved static rows carried forward: 5,464.
- Static certification complete: yes, for `ImportedDatabases` scope only.
- Runtime validation started: no.

Simulator verification:

- Python 3.14.6 detected.
- `python run_simulator.py summary` passed.
- `python run_simulator.py unknowns` passed and reported 27 unknown/unresolved rows.
- `python run_simulator.py search-room Silver` passed.
- `python run_simulator.py spell missile` passed.
- `python tests\smoke_test.py` passed after a local import-path repair in the simulator smoke test.

Boundaries:

- No canonical `Data/` rewrite.
- No imported/reference data deletion.
- No runtime field promotions.
- No server behavior changes.
- No gameplay systems.
- No live Worldgroup runtime validation claim.
- No exact combat, timer, script, AI, drop-rate, shop, or quest behavior certification.
- SimulatorBuild0002 is expected-model evidence only.

Next-scope candidate:

- After user push and green Actions, approve either a narrow SimulatorBuild0003 expected-model spawn/timer slice or a narrow server data-readiness slice that uses the simulator only as reference evidence.

## CodeUpdate-0049 Handoff

CodeUpdate-0049 is a documentation/reference preservation update for DataDecode0068 Spell Engine Proof.

Implemented:

- Added `DataDecodeCheckpoints/DataDecode0068/`.
- Added `DataDecodeCheckpoints/DataDecode0068/README.md`.
- Added `Reports/CodeUpdates/CodeUpdate-0049/DataDecode0068SpellEngineProofReport.md`.
- Refreshed `DataDecodeCheckpoints/UnifiedDecodedData/`; source inventory now has 1,701 rows.
- Recorded DataDecode0068 ZIP SHA-256 `587B92CE0495BF20C0619E4F2139CAA980F17A2EAE70A8C36A7A4162374CAFE6`.
- Recorded that DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.

DataDecode0068 metrics:

- Canonical spell records: 1,180.
- Native WCCSPELS records: 930.
- Native WCCSPELS string rows: 1,789.
- OmniVerse/WCC spell reference records: 1,350.
- Canonical `sourceId` to OmniVerse `legacy_number` spell matches: 1,151.
- Canonical spell raw field positions profiled: 41.
- Reference MHT files scanned: 10.
- Repo files with spell-term hits: 505.
- WGSERVER printable strings: 5,916.
- WGSERVER spell-term string rows: 46.
- WGSERVER spell constant probe rows: 439.
- Evidence matrix rows: 8.
- Field promotions: 0.

Boundaries:

- No `Data/` rewrite.
- No imported/reference deletion.
- No spell field promotions.
- No server behavior changes.
- No gameplay systems.
- No executable execution.
- No OmniVerse-only promotion.

Next-scope candidate:

- CodeUpdate-0049 was superseded by CodeUpdate-0050. Follow the current CodeUpdate-0050 handoff for next work.

## CodeUpdate-0048 Handoff

CodeUpdate-0048 is a documentation/reporting/tooling update for decoded-data consolidation and phone-friendly handoff.

Implemented:

- Added `Tools/DataDecode/Build-UnifiedDecodedData.ps1`.
- Generated `DataDecodeCheckpoints/UnifiedDecodedData/`.
- Added `Inventory/DecodedSourceInventory.csv` with 1,653 source inventory rows.
- Added `Summary/OmniVerseComparisonSummary.csv`.
- Added category comparison CSVs for classes, races, items, spells, and monsters.
- Added `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md`.
- Added `DataDecodeCheckpoints/PhoneHandoffTemplates/`.
- Recorded that phone ChatGPT work is structured handoff text only until Codex verifies and commits it.

Comparison snapshot:

- Classes: 15 canonical, 15 OmniVerse, 15 normalized-name matches.
- Races: 13 canonical, 13 OmniVerse, 13 normalized-name matches.
- Skills/abilities: 46 canonical skills versus 188 OmniVerse abilities; not equivalent yet.
- Items: 1,770 canonical, 1,950 OmniVerse, 1,948 OmniVerse item names present in canonical data by normalized name only.
- Spells: 1,180 canonical, 1,350 OmniVerse, 1,151 matched by `sourceId` to `legacy_number`.
- Shops: 0 canonical, 145 native decoded records, 178 OmniVerse shops.
- Monsters: 98 canonical starter/sample records, 1,100 OmniVerse monsters, 50 normalized-name matches.
- Rooms: 0 canonical, 1,246 OmniVerse room details, 168 OmniVerse roommaps.

Boundaries:

- No `Data/` rewrite.
- No imported/reference deletion.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No OmniVerse-only promotion.

Next-scope candidate:

- After user push and green Actions, continue DataDecode0051 Combat Routine Correlation from the GitHub repository working copy.

## CodeUpdate-0047 Handoff

CodeUpdate-0047 is a documentation/reference preservation and workflow transition update.

Implemented:

- Preserved `C:\Users\djkao\Downloads\DataDecode0049-WGSERVERRecordAccessStructureCorrelation-PreMirror.zip` contents under `DataDecodeCheckpoints/DataDecode0049/`.
- Recorded DataDecode0049 ZIP SHA-256: `8869C12860A5D645F5EBA5B8F6F51C9389A98E96A3ABA7B8CBB57288CCCE8A2B`.
- Recorded `C:\Worldgroup Server\wgserver.exe` metadata without copying or executing it: 432,128 bytes, SHA-256 `4DA28D718CF99701AA63C1889770F293BB3EC828AEA5E8BEA500473D2698C73F`.
- Recorded DataDecode0049 findings: 15 NativeRuntime DAT datasets profiled, 5,916 executable strings, 9,574 record constant hits, 29 string-address xrefs, 29 record-access candidate windows, 15 descriptor candidates, 93 canonical term hits, and 0 field promotions.
- Added `DataDecodeCheckpoints/DataDecode0050/DataDecode0050_GitHubTransitionHandoff.md`.
- Added `DataDecodeCheckpoints/DataDecode0050/DataDecode0050_Metrics.csv`.
- Recorded that future decode passes must update the GitHub repository directly; ZIPs are archival/checkpoint artifacts only.

Boundaries:

- No `Data/` rewrite.
- No imported/reference deletion.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No execution or copying of `wgserver.exe`.
- No public-ID to binary-row assumptions.
- No standalone DataDecode0050 ZIP was supplied in this Codex turn; dispatcher/message items are handoff continuity only until backed by committed reports or binary evidence.

Next-scope candidate:

- After user push and green Actions, continue with DataDecode0051 Combat Routine Correlation from the GitHub repository working copy. Focus on damage, armor, spell, resistance, and ability calculations. Promote no fields without binary evidence.

## CodeUpdate-0046 Handoff

CodeUpdate-0046 is a reference-capture/imported-evidence update. It records and imports the gathered OmniVerse/MudInfo validation payload before binary-first verification work.

Implemented:

- Recorded CodeUpdate-0045 GitHub Actions as green by user report.
- Recorded `E:\Httracks\OmniVerseCodexFull` as the HTTrack OmniVerse SPA shell/assets capture.
- Recorded that HTTrack alone does not enumerate full OmniVerse codex records because the site loads data through API calls.
- Imported `E:\Httracks\OmniVerseCodexApi\wcc` into `ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc` as reference evidence.
- Recorded WCC API capture counts/hashes for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommaps, 1,246 room details, and a 23-entry room capture error log.
- Imported MudInfo `maps.rar` source archive and 102 extracted map files under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/MudInfoMaps/`.
- Excluded exploratory HTTrack cache/test files from the committed payload to keep 0046 evidence clean.

Boundaries:

- Captured reference evidence copied only under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/`.
- No `Data/` rewrite.
- No imported/reference deletion.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No public-ID to binary-row assumptions.
- No exact MajorMUD parity claim.

Next-scope candidate:

- After user push and green Actions, run binary-first verification against the imported OmniVerse/MudInfo reference payload. Expand room coverage beyond the shop-seeded capture where endpoint seeds are still missing. Keep all OmniVerse/MudInfo data validation-only until binary evidence bridges public IDs to binary rows.

## CodeUpdate-0045 Handoff

CodeUpdate-0045 is a documentation/reference checkpoint integration. It preserves the user-provided DataDecode0043 Import Readiness Validation package under `DataDecodeCheckpoints/DataDecode0043/` and records its findings in continuity documents.

Implemented:

- Added the 20-file DataDecode0043 checkpoint under `DataDecodeCheckpoints/DataDecode0043/`.
- Recorded the checkpoint ZIP SHA-256: `0547361D07D8979F77F88B2D463E9AB720FC9851831AE107C369189FF51A5259`.
- Verified package contents and internal manifest entries.
- Recorded the known package limitation that `DataDecode0043_Manifest.csv` and checkpoint `UpdateManifest.json` do not match their own recorded manifest size/hash entries.
- Recorded key metrics: 96-97% static decode estimate, 6 canonical schemas checked, 16 repository JSON datasets checked, 63 repository JSON fields profiled, 1,000 foreign-key candidates reviewed, 83 native source files hashed, 5 import blockers registered, and 0 canonical field promotions.

Boundaries:

- No `Data/` rewrite.
- No imported data deletion.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.
- No editor implementation.
- No public-ID to binary-row assumptions.
- No exact MajorMUD parity claim.
- Codex did not push to GitHub.

Next-scope candidate:

- Push CodeUpdate-0045 and wait for green GitHub Actions. Then proceed to DataDecode0044 only if a full OmniVerse HTTrack mirror or runtime/debugging evidence is available. Otherwise, keep DataDecode0043 as the pre-mirror baseline and do not import candidate fields.

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

- CodeUpdate-0044 has been superseded by the CodeUpdate-0045 DataDecode0043 checkpoint handoff above.

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

CodeUpdate-0058 is the current local update candidate.

CodeUpdate-0057 is green by user report. CodeUpdate-0058 should be pushed by the user after the final local commit is ready.

## Next Recommended Work

1. Push CodeUpdate-0058 after the final local commit is ready and report GitHub Actions status.
2. After green Actions, choose one narrow non-gameplay Server construction slice.
3. Prefer either a module readiness report or first host configuration surface before gameplay behavior.
4. Keep hidden/scripted exits, formulas, timers, scripts, AI, drops, quests, and other runtime-only behavior marked uncertain until controlled validation or executable hooks exist.
5. Do not add gameplay systems without explicit approval.

## New Window Instruction

If this is a new ChatGPT or Codex session, do not ask the user to restate project history. Read `AGENTS.md`, this file, the roadmap, README, VERSION, CHANGELOG, ProjectManifest, and `DEVELOPMENT_PRINCIPLES.md`, inspect GitHub truth, then continue from the next recommended work unless the user gives different instructions.
