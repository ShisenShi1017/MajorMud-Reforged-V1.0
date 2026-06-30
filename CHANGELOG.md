# Changelog

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
