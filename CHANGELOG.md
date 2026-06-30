# Changelog

## CodeUpdate-0039 - Server Bootstrap Readiness Summary

- Added a compact host-tool readiness summary to the non-gameplay Server bootstrap report.
- Added readiness flags for project root, required directories, manifest identity, startup diagnostics, and host-tool readiness.
- Updated Server startup output to print readiness status while keeping gameplay status disabled.
- Added smoke-test coverage for readiness status.
- Folded in the Classic-first Server direction as planning/continuity documentation.
- Recorded CodeUpdate-0038 GitHub Actions as green by user report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, Reforged mode, Custom mode, AI systems, alternate gameplay rules, data rewrite, or `ImportedDatabases` overwrite is included.
- Local .NET 8.0.422 JSON parse, affected Release builds, smoke tests, and Server bootstrap run passed after Classic-first planning docs were folded in.

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
