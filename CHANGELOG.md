# Changelog

## CodeUpdate-0033 - Server Bootstrap Exit Code Contract

- Added an explicit `ExitCode` contract to the non-gameplay Server bootstrap report.
- Server startup now returns the bootstrap report exit code instead of duplicating success/failure code values.
- Smoke tests now verify bootstrap success exposes exit code 0 and bootstrap failure exposes exit code 1.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0032.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions remains pending until the user pushes CodeUpdate-0033 and reports the result.

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

- Recorded the user-reported green GitHub Actions result for CodeUpdate-0027.
- Added the initial Milestone 2 scope plan.
- Recommended CodeUpdate-0029 as host/server startup and runtime diagnostics foundation work, pending user approval.
- Updated continuity documents, manifests, and roadmap notes for Milestone 2 planning.
- Local .NET 8.0.422 restore/build/smoke-test verification passed.
- No gameplay feature work or implementation code was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.

## Earlier History

Earlier CodeUpdate history is preserved in repository reports and handoff documents. Current sessions should continue from the latest verified Code Update recorded at the top of this file.
