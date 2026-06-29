# Changelog

## CodeUpdate-0030 - Truth Documentation Cleanup and Agent Handoff Rules

- Added root `AGENTS.md` with mandatory handoff rules for ChatGPT, Codex, and future developers.
- Recorded that GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the authoritative source of truth.
- Added the required mode check: Codex mode updates local only and waits for user push plus stated green GitHub Actions before continuing; ChatGPT mode uses verified patch ZIPs for local application.
- Added the permanent rule: never guess when creating, fixing, reviewing, packaging, or reporting an update.
- Updated continuity documents to remove or replace stale source-of-truth and Milestone 1/old CodeUpdate direction.
- Updated Server documentation to reflect CodeUpdate-0029 startup-only diagnostics.
- Added a CodeUpdate-0030 report.
- No source implementation code or gameplay features were added.
- GitHub Actions remains pending until the user applies, commits, and pushes this documentation patch.

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

## CodeUpdate-0026 - Milestone 1 Finalization

- Added the Milestone 1 verification report under `Reports/CodeUpdates/CodeUpdate-0026/`.
- Updated continuity documents and manifests for the Milestone 1 finalization candidate.
- Recorded local .NET 8.0.422 restore/build/smoke-test verification.
- Prepared Milestone 2 roadmap direction without starting gameplay or engine expansion work.
- No BAT installer, update ZIP, or GitHub push was created by Codex.
- Local CodeUpdate-0026 restore/build/smoke-test verification passed; GitHub Actions remained pending until user push.

## CodeUpdate-0025 - Rebrand Completion Audit

- Completed a rebrand audit for active source, project, assembly, namespace, workflow, and importer utility references.
- Removed obsolete duplicate Tools/Importer/MajorMudDatDecoder.py; Tools/Importer/ReforgedDatDecoder.py remains the active DAT inspection tool.
- Recorded that remaining MajorMud references are historical, repository-name, or legacy-context references rather than active source/build identifiers.
- No gameplay feature work was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.
- Local .NET 8.0.422 restore/build/smoke-test verification passed; GitHub Actions remains the official gate after user push.

## CodeUpdate-0024B - Core Startup Diagnostics Logger Fix

- Corrected the CoreStartupDiagnostics logger call to use the verified InMemoryReforgedLogger.Write(LogSeverity.Information, ...) API.
- Fixed the CodeUpdate-0024 build failure caused by calling Info on the concrete in-memory logger type.
- Recorded CodeUpdate-0024B as a direct corrective patch with no gameplay feature work.
- No BAT installer or update package was created per current user instruction.

## CodeUpdate-0024 - Core Verification

- Added dependency-free Core event bus contracts and in-memory implementation.
- Added Core startup diagnostics for DI, configuration, event publishing, and repository health.
- Added `Tests/Source/Reforged.Core.Tests` smoke-test project.
- Updated GitHub Actions to restore, build, and run smoke tests.
- No gameplay feature work was introduced.
