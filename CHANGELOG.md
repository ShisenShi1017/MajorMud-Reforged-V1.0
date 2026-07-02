# Changelog

## CodeUpdate-0052 - Build0027 Current-State Handoff

- Recorded CodeUpdate-0051 GitHub Actions as green by user report.
- Inspected `MajorMUD-Reforged-CodexHandoff-CurrentState-Build0027.zip`.
- Recorded Build0027 ZIP SHA-256 `7932FA3E6D4664D5440C0039CBF7FD85528CB0A5CBC09C7BE3C25CB2E4CF750F`.
- Preserved the full Build0027 handoff under `DataDecodeCheckpoints/CurrentStateBuild0027/`.
- Preserved human-readable Build0027 docs, tasks, and evidence manifest under `Docs/BehavioralRecovery/Build0027/`.
- Added `Reports/CodeUpdates/CodeUpdate-0052/Build0027CurrentStateHandoffReport.md`.
- Recorded that static data/content recovery and entity/world/catalog recovery are effectively complete by handoff statement, while original runtime validation remains unavailable.
- Recorded that Build0024/0025 matrices remain `expected_model` until converted into actual executable simulator tests.
- Recorded that prior chat pass/fail claims are not authoritative unless backed by artifact logs or locally generated test output.
- No executable harness implementation, canonical `Data` rewrite, server behavior change, field promotion, gameplay system, or original runtime behavior certification is included.

## CodeUpdate-0051 - Import Staging and Build0021 Behavioral Recovery

- Recorded CodeUpdate-0050 GitHub Actions as green by user report.
- Added `Tools/DataDecode/Build-ImportStagingReport.ps1`.
- Generated `DataDecodeCheckpoints/ImportStaging/` with canonical data surface, simulator evidence surface, import staging queue, phone handoff template, manifest, README, and report files.
- Preserved the user-provided Build0021 behavioral recovery evidence package under `Docs/BehavioralRecovery/Build0021/` and `DataDecodeCheckpoints/SimulatorBuild0021/`.
- Added `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`.
- Added `Core/Runtime/BehavioralRecovery/README.md`.
- Added `Reports/CodeUpdates/CodeUpdate-0051/ImportStagingAndBuild0021Report.md`.
- Recorded import staging counts: 1,246 evidence-backed room records, 178 shops, 732 monster item drop edges, 125 quest/progression candidates, 46 skill/trainer-related rows, 1,770 item/equipment records, 1,180 spell records, and 1,100 expected monster records.
- Recorded Build0021 formula/catalog confidence counts: 6 `verified_from_data`, 4 `strong_candidate`, 2 `weak_candidate`, and 4 `runtime_required`.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, live runtime validation claims, or Build0021 formula certifications are included.

## CodeUpdate-0050 - SimulatorBuild0002 Handoff and DataDecode0110 Static Certification

- Preserved the user-provided SimulatorBuild0002 Codex handoff under `DataDecodeCheckpoints/SimulatorBuild0002/`.
- Extracted final DataDecode0110 static certification files under `DataDecodeCheckpoints/DataDecode0110/`.
- Added selected DataDecode0099-0108 DAT decoding decision docs under `Docs/DATDecoding/`.
- Installed the expected-model simulator under `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`.
- Recorded source package SHA-256 `66DF6BF0E1885A96C1B3BFB9A9D535FEDA8FA9D025A807509175B4C644D758DA`.
- Recorded DataDecode0110 metrics: 3,908 imported database files checked, 1 MDB present, 5 CSV files present, 0 unsupported inferences, 3,975 unresolved ownership rows carried forward, and 5,464 unresolved static rows carried forward.
- Recorded that SimulatorBuild0002 is expected-model evidence only and does not certify live Worldgroup behavior.
- Excluded generated Python `__pycache__` files and the embedded stale full repository archive.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, or live runtime validation are included.

## CodeUpdate-0049 - DataDecode0068 Spell Engine Proof

- Preserved the user-provided DataDecode0068 Spell Engine Proof checkpoint under `DataDecodeCheckpoints/DataDecode0068/`.
- Recorded DataDecode0068 package SHA-256 `587B92CE0495BF20C0619E4F2139CAA980F17A2EAE70A8C36A7A4162374CAFE6`.
- Recorded spell proof metrics: 1,180 canonical spells, 930 native WCCSPELS records, 1,789 native WCCSPELS string rows, 1,350 OmniVerse spell reference records, 1,151 canonical-to-OmniVerse spell matches, 41 raw field positions, 439 WGSERVER spell constant probes, and 0 field promotions.
- Refreshed `DataDecodeCheckpoints/UnifiedDecodedData/`; the source inventory now records 1,701 rows.
- Recorded that DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.
- No canonical `Data` rewrites, server behavior changes, spell field promotions, gameplay systems, executable execution, or OmniVerse-only promotions are included.

## CodeUpdate-0048 - Unified Decoded Data and Phone Workflow

- Added `Tools/DataDecode/Build-UnifiedDecodedData.ps1` to generate a repo-native decoded-data inventory and OmniVerse comparison surface.
- Added `DataDecodeCheckpoints/UnifiedDecodedData/` with generated manifest, source inventory, category summary, and comparison CSV reports.
- Added Android ChatGPT decode workflow docs under `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md`.
- Added phone handoff templates under `DataDecodeCheckpoints/PhoneHandoffTemplates/`.
- Recorded comparison findings: classes/races have full normalized-name coverage, spells have partial `sourceId` coverage, items are name-only correlation, and shops/rooms/monsters remain incomplete as Reforged-native canonical data.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, or imported/reference deletions are included.

## CodeUpdate-0047 - DataDecode GitHub Transition

- Preserved the user-provided DataDecode0049 WGSERVER record access / structure correlation checkpoint under `DataDecodeCheckpoints/DataDecode0049/`.
- Recorded DataDecode0049 package SHA-256 `8869C12860A5D645F5EBA5B8F6F51C9389A98E96A3ABA7B8CBB57288CCCE8A2B`.
- Recorded `wgserver.exe` reference hash `4DA28D718CF99701AA63C1889770F293BB3EC828AEA5E8BEA500473D2698C73F` without copying or executing the binary.
- Added DataDecode0050 GitHub transition handoff and metrics under `DataDecodeCheckpoints/DataDecode0050/`.
- Updated continuity documents so future decode passes use the GitHub repository as the direct working copy; ZIPs are archival/checkpoint artifacts only.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, executable execution, or public-ID to binary-row assumptions are included.

## CodeUpdate-0046 - OmniVerse Capture Checkpoint

- Recorded CodeUpdate-0045 as green by user report.
- Recorded the HTTrack OmniVerse SPA shell/assets capture under `E:\Httracks\OmniVerseCodexFull`.
- Imported the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only.
- Recorded direct WCC OmniVerse API capture files for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommaps, 1,246 room details, and a 23-entry room capture error log.
- Recorded and preserved MudInfo `maps.rar` archive hash, source archive, and 102 extracted MudInfo map files.
- Documented that HTTrack alone does not capture OmniVerse codex records because the site loads data through API calls.
- Documented that the committed reference payload excludes exploratory HTTrack cache/test files to keep final 0046 evidence clean.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, or public-ID to binary-row assumptions are included.

## CodeUpdate-0045 - DataDecode0043 Checkpoint Integration

- Added the user-provided `DataDecode0043-ImportReadinessValidation-PreMirror` checkpoint under `DataDecodeCheckpoints/DataDecode0043/`.
- Recorded DataDecode0043 as the pre-mirror import-readiness baseline: 96-97% static decode estimate, 6 canonical schemas checked, 16 repository JSON datasets checked, 1,000 foreign-key candidates reviewed, 83 native source files hashed, 5 import blockers registered, and 0 canonical field promotions.
- Documented that original MajorMUD binary files remain authoritative and OmniVerse/public IDs must not be assumed to equal binary record indexes.
- Documented package verification limitations: `DataDecode0043_Manifest.csv` and checkpoint `UpdateManifest.json` do not match their own recorded manifest size/hash entries.
- No server behavior, gameplay, networking, editor implementation, canonical `Data` rewrite, imported data deletion, field promotion, or production import is included.

## CodeUpdate-0044 - Original Reference Extractor

- Added `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1` for reference-only capture of original Worldgroup, WCC, WBB, WSGSERVE, and related source material.
- Added source preservation, ZIP extraction, optional 7-Zip-backed RAR/7z extraction, SHA-256 file inventory, category hints, printable string extraction, and JSON/CSV/Markdown reports.
- Added `Tools/ReferenceCapture/README.md` with safe usage and extraction boundaries.
- Recorded CodeUpdate-0043 GitHub Actions as green by user report.
- Preserved the rule that imported/reference outputs are evidence only and cannot overwrite canonical `Data/` without a reviewed follow-up update.
- No gameplay systems, combat, character creation, world simulation, networking expansion, editor implementation, canonical `Data` rewrite, imported data deletion, original executable execution, keygen use, license bypass, or exact parity claim is included.

## CodeUpdate-0043 - Server Data Readiness Classification

- Added runtime-readiness classification to canonical Server data catalog summaries.
- Separated file presence, record loading, placeholder status, runtime-ready status, and readiness notes.
- Updated Server startup output to report runtime-ready and runtime-blocked category counts.
- Added smoke-test coverage for placeholder categories and runtime-readiness classification.
- Recorded the long-term goal to retire `ImportedDatabases/` only after Reforged-native `Data/` is complete, verified, and explicitly approved for cleanup.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, WCC import, imported data deletion, proprietary binary parser, or exact parity claim is included.

## CodeUpdate-0042 - Server Data Catalog Bootstrap

- Added read-only Server data catalog loading through the existing Core `CoreDataRepositoryLoader`.
- Attached canonical `Data/` category readiness to non-gameplay Server bootstrap.
- Updated Server startup output to print canonical data category status, file sizes, and loaded record counts.
- Added smoke-test coverage for Server data catalog attachment and direct catalog audit loading.
- Local bootstrap loads 13/13 registered categories and 4,527 records, with 5 empty placeholder categories.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, WCC import, proprietary binary parser, or exact parity claim is included.

## CodeUpdate-0041 - Classic Parity Audit Foundation

- Added read-only Classic parity audit inventory for canonical `Data/` and imported reference material.
- Added broad category summaries for rooms, monsters, items, spells, shops, quests, classes/races, ganghouses, maps/paths, and raw legacy databases.
- Attached the audit to non-gameplay Server bootstrap and startup output.
- Added smoke-test coverage for audit inventory and bootstrap attachment.
- Recorded that all WCC data is reference-only unless the user explicitly approves a reviewed import/reconciliation update.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, imported-reference overwrite, proprietary binary parser, or exact parity claim is included.

## CodeUpdate-0040 - WCC-Labeled WBB Imported Reference Archive

- Preserved the user-provided `wbbmud.zip` archive under `ImportedDatabases/WCCLabeledWBB/` as reference/import material.
- Added archive manifest, file inventory, source archive, extracted reference files, and import report.
- Recorded that the archive is WCC-labeled WBB because the source BBS hosted two different games.
- No canonical `Data/` rewrite, gameplay implementation, runtime dependency on legacy executables/DLLs, or overwrite of existing imported archives is included.

## CodeUpdate-0039 - Server Bootstrap Readiness Summary

- Added a compact host-tool readiness summary to the non-gameplay Server bootstrap report.
- Added readiness flags for project root, required directories, manifest identity, startup diagnostics, and host-tool readiness.
- Updated Server startup output to print readiness status while keeping gameplay status disabled.
- Added smoke-test coverage for readiness status.
- Folded in the Classic-first Server direction as planning/continuity documentation.
- Folded in engine-first architecture direction as planning/continuity documentation.
- Added the user-provided WCC default install under `ImportedDatabases/WCCDefaultInstall/` as reference/import material.
- Recorded CodeUpdate-0038 GitHub Actions as green by user report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, Reforged mode, Custom mode, AI systems, alternate gameplay rules, canonical `Data` rewrite, or `ImportedDatabases` overwrite is included.
- Local .NET 8.0.422 JSON parse, affected Release builds, smoke tests, Server bootstrap run, and WCC default archive integrity verification passed after folded documentation/import work.

## CodeUpdate-0038 - Server Bootstrap Directory Status

- Added required-directory status records to the non-gameplay Server bootstrap report.
- Added required-directory total, present, and missing counts for future host/editor tooling.
- Updated Server startup output to print required-directory summary and detail lines.
- Added smoke-test coverage for required-directory status.
- Recorded CodeUpdate-0037 GitHub Actions as green by user report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, or `ImportedDatabases` overwrite is included.
- Local .NET 8.0.422 JSON parse, restore, sequential Release build, smoke tests, and Server bootstrap run passed.
- GitHub Actions is green by user report.

## CodeUpdate-0037 - Software Design Specification and Continuity Cleanup

- Combined the user-provided CodeUpdate-0036B continuity cleanup package and CodeUpdate-0037 Software Design Specification package into one local documentation update.
- Fetched GitHub truth and fast-forwarded local `main` to CodeUpdate-0036 before applying the combined update.
- Repaired stale continuity documents that still referenced CodeUpdate-0035 after CodeUpdate-0036 direct-GitHub commits.
- Added draft `Docs/SDS/` Software Design Specification planning records for future AI, combat, quest, explorer, discovery map, launcher, host editor, loot, networking, plugin, expansion, data-format, gameplay-flag, and ganghouse systems.
- Added CodeUpdate-0037 documentation and verification report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, source-code behavior changes, data rewrite, or third-party raw archive import is included.
- JSON parse and local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0036 - Knowledge Base and Design Decisions

- Added a CodeUpdate-0036 documentation plan for the Reforged knowledge base, design decisions, and reference-source policy.
- Added `KnowledgeBase/DesignDecisions/DesignDecisionIndex.md` to track accepted/proposed design decisions.
- Added `KnowledgeBase/Research/ReferenceSourceCatalog.md` to catalog WCC, WSGSERVE, LLT XML, MegaMUD, Mud Hex, OmniVerse, MudInfo, MudCentral, Reddit/community sources, and other verified references.
- Added `KnowledgeBase/MeetingNotes/CodeUpdate-0036-Discussion-Recap.md` to preserve the major design discussion after CodeUpdate-0035.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0035.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, AI runtime, data rewrite, or raw third-party binary/reference archive import was added.
- CodeUpdate-0036 is documentation/research only; no local build was run in ChatGPT.
- GitHub Actions remains pending until the user verifies this direct GitHub documentation update.

## CodeUpdate-0035 - Server Bootstrap Validation Summary

- Added validation issue-count properties to the non-gameplay Server bootstrap report.
- Server bootstrap console output now includes a validation summary by severity.
- Smoke tests verify success and failure bootstrap paths expose expected validation issue counts.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0034.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, or data rewrite was added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0034 - OmniVerse Reference Audit Plan

- Recorded OmniVerse as a read-only reference source for future Reforged data audits.
- Documented verified public reference endpoints for MajorMUD world/status, codex item/spell samples, and shop/trainer data.
- Added the rule that OmniVerse may suggest corrections but must not automatically overwrite canonical Reforged `Data/`.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0033.
- Updated continuity documents, manifests, release notes, roadmap, and audit report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, or data rewrite was added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- No files under `Data/` or `ImportedDatabases/` were changed.
- GitHub Actions is green by user report.

## CodeUpdate-0033 - Server Bootstrap Exit Code Contract

- Added an explicit `ExitCode` contract to the non-gameplay Server bootstrap report.
- Server startup now returns the bootstrap report exit code instead of duplicating success/failure code values.
- Smoke tests now verify bootstrap success exposes exit code 0 and bootstrap failure exposes exit code 1.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0032.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0032 - Server Bootstrap Manifest Identity

- Added ProjectManifest identity capture to the non-gameplay Server bootstrap report.
- Server bootstrap output now includes project, CodeUpdate, database version, and milestone values.
- Added smoke-test coverage to verify the bootstrap captures manifest identity fields.
- Updated Server documentation and continuity records.
- Added a handoff improvement: future updates should record a short next-scope candidate and explicit boundaries separately from completed work.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0031A - Server Smoke Test Compile Fix

- Corrected invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs`.
- Fixed the GitHub Actions test build failure reported at `Program.cs#L59` with `Syntax error, ',' expected`.
- Recorded CodeUpdate-0031A as a corrective build-fix patch for CodeUpdate-0031.
- Updated continuity documents, release notes, ProjectManifest, roadmap, and this changelog.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- User reported CodeUpdate-0031A GitHub Actions green.
- Codex local .NET 8.0.422 sequential restore/build/smoke verification passed.

## CodeUpdate-0031 - Server Bootstrap Foundation

- Added `Apps/Source/Reforged.Server/Program.cs` as the first non-gameplay Server entry point.
- Added `ServerBootstrap` and `ServerBootstrapReport` to verify repository root, required folders, ProjectManifest identity, and existing Server startup diagnostics.
- Updated `Reforged.Server.csproj` to build as an executable project.
- Added a Server bootstrap smoke test to `Tests/Source/Reforged.Core.Tests/Program.cs`.
- Added `DEVELOPMENT_PRINCIPLES.md` to separate engineering principles from agent workflow rules.
- Updated continuity documents, roadmap, release notes, ProjectManifest, and Server source README.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- GitHub Actions failed after push due to a syntax error in the smoke-test JSON string; CodeUpdate-0031A corrects this.

## CodeUpdate-0030 - Truth Documentation Cleanup and Agent Handoff Rules

- Added root `AGENTS.md` with mandatory handoff rules for ChatGPT, Codex, and future developers.
- Recorded that GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the authoritative source of truth.
- Added the required mode check: Codex mode updates local only and waits for user push plus stated green GitHub Actions before continuing; ChatGPT mode uses verified patch ZIPs for local application.
- Added the permanent rule: never guess when creating, fixing, reviewing, packaging, or reporting an update.
- Updated continuity documents to remove or replace stale source-of-truth and Milestone 1/old CodeUpdate direction.
- Updated Server documentation to reflect CodeUpdate-0029 startup-only diagnostics.
- Added a CodeUpdate-0030 report.
- No source implementation code or gameplay features were added.
- The user reported GitHub Actions green for CodeUpdate-0030.

## CodeUpdate-0029 - Host Server Startup Diagnostics

- Added startup-only Server diagnostics for the first Milestone 2 implementation step.
- Wired Server startup checks through existing Core configuration, logging, service registry, event bus, repository health, and startup diagnostics.
- Added a Server startup diagnostics smoke test.
- Updated the Server README and continuity documents.
- Local .NET 8.0.422 restore/build/core-server-smoke verification passed.
- No gameplay systems, combat, character creation, world simulation, editor implementation, or networking expansion were added.

## CodeUpdate-0028 - Milestone 2 Scope Plan

- Recorded the user-reported green CodeUpdate-0027 GitHub Actions result.
- Added the initial Milestone 2 scope plan.
- Recommended CodeUpdate-0029 as host/server startup and runtime diagnostics foundation work, pending user approval.
- Updated continuity documents, manifests, and roadmap notes for Milestone 2 planning.
- Local .NET 8.0.422 restore/build/smoke-test verification passed.
- No gameplay feature work or implementation code was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.

## Earlier History

Earlier CodeUpdate history is preserved in repository reports and handoff documents. Current sessions should continue from the latest verified Code Update recorded at the top of this file.
