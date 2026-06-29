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
8. Inspect relevant repository files before making changes.
9. Continue from the latest verified Code Update recorded here.

## Current Repository Facts

| Field | Value |
|---|---|
| Project | Reforged |
| GitHub Truth | `ShisenShi1017/MajorMud-Reforged-V1.0` |
| Current Milestone | Milestone 2 - Engine Modernization / Host & World Editor Planning |
| Current Code Update | 0030 |
| Current Database Version | 0009 |
| Last Update Name | CodeUpdate-0030-Truth-Documentation-Cleanup |
| Milestone Estimate | Milestone 1 frozen; Milestone 2 implementation has started but should proceed in small verified slices |
| Source of Truth | Current GitHub repository state |
| Runtime Policy | No .NET SDK required for normal runtime |
| ChatGPT Packaging Policy | Patch-only ZIP with exactly one top-level update folder, included Apply-CodeUpdate BAT, and UpdateManifest.json |
| Codex Policy | Codex updates local only and waits for user push plus stated green GitHub Actions before continuing |

## Current Development Goal

Milestone 1 is frozen. Milestone 2 implementation started in CodeUpdate-0029 with constrained host/server startup diagnostics. CodeUpdate-0030 cleans truth documentation and creates `AGENTS.md` so ChatGPT, Codex, and future handoffs follow the same rules.

Do not add gameplay systems without explicit user approval.

## Permanent Project Rules

- GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the source of truth.
- Before starting a Code Update, inspect GitHub truth and verify current milestone/update/database state.
- Never guess when creating, fixing, reviewing, or reporting updates.
- If something cannot be verified, state that clearly.
- Do not claim code, builds, packages, executables, or GitHub Actions results exist unless verified.
- The repository layout is frozen unless the user explicitly approves a structural change.
- `Data/` is the authoritative game database after import.
- `ImportedDatabases/` is archive/reference only.
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
| Engine modernization | Planning/early implementation | Continue in small verified slices. |
| Host/world editor | Planned | Do not begin larger editor work without approval. |
| Gameplay systems | Not started | Requires explicit user approval. |
| Networking expansion | Not started | Requires explicit user approval. |

## Update History Summary

| Code Update | Summary |
|---|---|
| 0030 | Added `AGENTS.md`, corrected source-of-truth and mode rules, removed stale continuity direction, and recorded the never-guess creation rule. |
| 0029 | Added startup-only Server diagnostics and a smoke test for the first constrained Milestone 2 implementation slice. |
| 0028 | Recorded user-reported green CodeUpdate-0027 GitHub Actions and created the first Milestone 2 scope plan. |
| 0027 | Froze the Milestone 1 Core Framework baseline after user-reported green CodeUpdate-0026B GitHub Actions and prepared the Milestone 2 planning handoff. |
| 0026B | Cleaned generated artifacts, repaired ignore rules, and fixed visible documentation encoding damage before baseline freeze. |
| 0026 | Added Milestone 1 verification report, refreshed continuity docs, and prepared final freeze pending GitHub Actions. |
| 0025 | Completed active rebrand audit, removed obsolete duplicate MajorMud-named importer utility, and confirmed no gameplay changes. |
| 0024B | Corrected CoreStartupDiagnostics to use the verified Write(LogSeverity.Information, ...) logger API. |
| 0024 | Added Core verification diagnostics, event bus work, and smoke-test project. |

## CodeUpdate-0030 Handoff

CodeUpdate-0030 is documentation/governance only. It adds `AGENTS.md`, updates truth documents, records the Codex-vs-ChatGPT workflow split, and makes the no-guess creation rule explicit.

Implemented:

- `AGENTS.md`
- Updated `AI_CONTEXT.md`
- Updated `README.md`
- Updated `CHANGELOG.md`
- Updated `VERSION.md`
- Updated `ProjectManifest.json`
- Updated `ReleaseNotes.md`
- Updated `Docs/Architecture/PROJECT_ROADMAP.md`
- Updated `Apps/Server/README.md`
- Added `Reports/CodeUpdates/CodeUpdate-0030/CodeUpdate-0030-Truth-Documentation-Cleanup.md`

Boundaries:

- No source implementation code.
- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No networking expansion.

## Verification Status

Verified before creating CodeUpdate-0030:

- GitHub repository access was available.
- Current truth documents showed CodeUpdate-0029 and Milestone 2 state.
- Current CoreStartupDiagnostics logger fix was present in GitHub.
- GitHub Actions workflow path exists and restores/builds Core, app projects, and tests.
- Stale source-of-truth and milestone text existed in current docs and was targeted by this docs-only update.

Package verification required before delivery:

- ZIP exists.
- ZIP contains exactly one top-level folder.
- ZIP contains `Apply-CodeUpdate-0030.bat`.
- ZIP contains `UpdateManifest.json`.
- ZIP contains patch payload only.

GitHub Actions remains pending until the user applies, commits, and pushes CodeUpdate-0030.

## Next Recommended Work

1. Apply CodeUpdate-0030 locally.
2. Commit and push to GitHub.
3. Confirm GitHub Actions is green for the current push.
4. After green Actions, approve the next small Milestone 2 host/server scope.
5. Do not add gameplay systems without explicit approval.

## New Window Instruction

If this is a new ChatGPT or Codex session, do not ask the user to restate project history. Read `AGENTS.md`, this file, the roadmap, README, VERSION, CHANGELOG, and ProjectManifest, inspect GitHub truth, then continue from the next recommended work unless the user gives different instructions.
