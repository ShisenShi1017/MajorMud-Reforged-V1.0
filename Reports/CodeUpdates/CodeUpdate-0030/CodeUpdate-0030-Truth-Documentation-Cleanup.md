# CodeUpdate-0030 - Truth Documentation Cleanup and Agent Handoff Rules

## Purpose

Create a single efficient handoff rule file and clean continuity documents so ChatGPT, Codex, and future sessions do not disagree about source of truth, work mode, packaging, or verification rules.

## Verified Before Creation

The following GitHub truth files were reviewed before creating this update:

- `README.md`
- `AI_CONTEXT.md`
- `VERSION.md`
- `CHANGELOG.md`
- `ProjectManifest.json`
- `ReleaseNotes.md`
- `Docs/Architecture/PROJECT_ROADMAP.md`
- `.github/workflows/dotnet-build.yml`
- `Core/Source/Reforged.Core/Diagnostics/CoreStartupDiagnostics.cs`
- `Apps/Server/README.md`

Findings:

- Current GitHub truth was CodeUpdate-0029 / Milestone 2.
- Milestone 1 was recorded as frozen.
- CodeUpdate-0024B logger fix was present in GitHub.
- GitHub Actions workflow exists and restores/builds Core, application projects, and tests.
- Some continuity text still referenced stale uploaded-ZIP source-of-truth language or old Milestone 1 next tasks.
- Server app documentation lagged behind CodeUpdate-0029 diagnostics work.

## Changed Files

- Added `AGENTS.md`.
- Replaced `AI_CONTEXT.md` with a concise current handoff document.
- Replaced `README.md` with a current project overview and handoff rule summary.
- Updated `CHANGELOG.md`.
- Updated `VERSION.md`.
- Updated `ProjectManifest.json`.
- Updated `ReleaseNotes.md`.
- Updated `Docs/Architecture/PROJECT_ROADMAP.md`.
- Updated `Apps/Server/README.md`.

## Rules Added or Clarified

- GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the authoritative source of truth.
- Each chat window must verify whether it is Codex mode or ChatGPT mode before making changes.
- Codex updates local only and waits for the user to push and state current GitHub Actions is green before continuing.
- ChatGPT uses verified ZIP patch workflow for local updates unless explicitly directed otherwise.
- Never guess when creating, fixing, reviewing, packaging, or reporting updates.

## Boundaries

This update is documentation/governance only.

It does not add:

- Source implementation code
- Gameplay systems
- Combat
- Character creation
- World simulation
- Networking expansion
- Editor implementation

## Verification

Package verification performed before delivery:

- ZIP exists.
- ZIP contains exactly one top-level folder.
- Top-level folder contains `Apply-CodeUpdate-0030.bat`.
- Top-level folder contains `UpdateManifest.json`.
- Payload is patch-only and contains only changed/new documentation/governance files.

GitHub Actions remains pending until the user applies, commits, and pushes this update.
