# Changelog

## CodeUpdate-0031 - Server Bootstrap Foundation

- Added `Apps/Source/Reforged.Server/Program.cs` as the first non-gameplay Server entry point.
- Added `ServerBootstrap` and `ServerBootstrapReport` to verify repository root, required folders, ProjectManifest identity, and existing Server startup diagnostics.
- Updated `Reforged.Server.csproj` to build as an executable project.
- Added a Server bootstrap smoke test to `Tests/Source/Reforged.Core.Tests/Program.cs`.
- Added `DEVELOPMENT_PRINCIPLES.md` to separate engineering principles from agent workflow rules.
- Updated continuity documents, roadmap, release notes, ProjectManifest, and Server source README.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- Local .NET restore/build/test could not be run in the ChatGPT packaging container because the .NET SDK is not installed; GitHub Actions remains the official validation gate after push.

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

- Recorded the user-reported green GitHub Actions result for CodeUpdate-0027.
- Added the initial Milestone 2 scope plan.
- Recommended CodeUpdate-0029 as host/server startup and runtime diagnostics foundation work, pending user approval.
- Updated continuity documents, manifests, and roadmap notes for Milestone 2 planning.
- Local .NET 8.0.422 restore/build/smoke-test verification passed.
- No gameplay feature work or implementation code was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.

## CodeUpdate-0027 - Milestone 1 Baseline Freeze

- Recorded the user-reported green GitHub Actions result for CodeUpdate-0026B.
- Marked Milestone 1 - Core Framework as complete/frozen.
- Updated continuity documents, manifests, and roadmap notes for the Milestone 2 planning handoff.
- Added the CodeUpdate-0027 baseline freeze report and update note.
- Local .NET 8.0.422 restore/build/smoke-test verification passed.
- No gameplay feature work was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.

## CodeUpdate-0026B - Baseline Cleanup

- Removed generated source-project `bin/` and `obj/` build artifacts from the local workspace.
- Added targeted `.gitignore` rules for source/test project build outputs while preserving the intentional root `Bin/` runtime handoff folder.
- Repaired visible mojibake/encoding damage in continuity documents.
- Recorded this cleanup as a Milestone 1 baseline hygiene patch before final freeze.
- Local .NET 8.0.422 restore/build/smoke-test verification passed.
- No gameplay feature work was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.

## Earlier History

Earlier CodeUpdate history is preserved in repository reports and handoff documents. Current sessions should continue from the latest verified Code Update recorded at the top of this file.
