# AI Context for Reforged

## READ THIS FIRST

This is the authoritative handoff document for every new ChatGPT window and every development continuation. Read this file before modifying the repository.

Reforged is a standalone game engine and toolset inspired by the original legacy MUD. Original legacy MUD files are import/reference material only. After import, `Data/` is the live editable game database and `ImportedDatabases/` is archive/reference only.

## Required Session Startup Order

1. Read `AI_CONTEXT.md` completely.
2. Read `Docs/Architecture/PROJECT_ROADMAP.md`.
3. Read `README.md`.
4. Read `VERSION.md`.
5. Read `CHANGELOG.md`.
6. Read `ProjectManifest.json`.
7. Inspect the repository before making changes.
8. Continue from the latest verified Code Update recorded here.

## Current Repository Facts

| Field | Value |
|---|---|
| Project | Reforged |
| Current Milestone | Milestone 1 - Core Framework |
| Current Code Update | 0026 |
| Current Database Version | 0009 |
| Last Update Name | CodeUpdate-0026-Milestone-1-Finalization |
| Milestone Estimate | 98-99% complete; final freeze pending CodeUpdate-0026 GitHub Actions |
| Source of Truth | GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` and latest applied local patch state |
| Runtime Policy | No .NET SDK required for normal runtime |
| Update Packaging Policy | Patch-only ZIP with exactly one top-level update folder and included Apply-CodeUpdate BAT |


## CodeUpdate-0019 Current Handoff Note

CodeUpdate-0019 performs the controlled project rebrand requested by the user after CodeUpdate-0018 was verified in GitHub Actions. Treat `Reforged.*` as the active source namespace and project naming pattern. The GitHub Actions workflow now restores and builds the renamed `Reforged.*` projects. The CodeUpdate-0018 Core application contract compile fix must remain in `Core/Source/Reforged.Core/Reforged.Core.csproj`.

## Current Development Goal

Continue Milestone 1 until CodeUpdate-0026 is pushed and GitHub Actions is green. Do not begin Milestone 2 implementation until the Milestone 1 baseline is frozen after that official gate.

## Milestone 1 Deliverable Status

| Deliverable | Status | Notes |
|---|---:|---|
| Core configuration system | Verified locally | Smoke tests verify defaults and flattened JSON loading. |
| Core logging system | Verified locally | Logger contract and in-memory logger build; 0024B repaired startup diagnostics logging call. |
| Validation framework | Verified locally | Data JSON, cross-reference, and startup diagnostics validation build and smoke-test. |
| Serialization framework | Verified locally | JSON data loading foundations build and repository health smoke test passes. |
| Repository interfaces | Verified locally | Repository-backed loaders and health checks build; repository health smoke test passes. |
| Shared data models | Substantially underway | Shared DataRecord/IReforgedDataRecord/GameEntity foundations reduce app-specific model drift. |
| Event system | Verified locally | Smoke test verifies publish and subscription disposal behavior. |
| Dependency injection/service registry | Verified locally | Smoke test resolves registered services. |
| Version management | Substantially underway | Version info and version manager exist. |
| Project manifest reader | Substantially underway | Shared manifest model/reader and manifest policy fields exist. |
| Repository health checker | Nearly complete | Health models plus update package, app Core-reference, Data JSON, Data cross-reference, and duplicate model checks exist. |
| Shared utility library | In progress | Path safety exists; more utilities should be added only when needed. |
| Unit-test framework | Verified locally | `Reforged.Core.Tests` runs Milestone 1 smoke tests successfully. |
| Applications using Core | Verified locally | Server, Importer, Editor, Launcher, and Client source projects restore/build against Core. |

## Milestone 1 Completion Checklist

| Requirement | Status | Evidence / Next Action |
|---|---:|---|
| No duplicated models across applications | Verified locally | Duplicate model enforcement is part of startup diagnostics/repository health verification. |
| Every application references Core | Verified locally | All application source projects restore/build with Core project references. |
| Core has no dependencies on application projects | Verified by structure | Keep dependency direction one-way into Core. |
| Core configuration fully functional | Verified locally | Configuration smoke test passed. |
| Core logging complete | Substantially underway | Add file/runtime sink expectations when Bin artifacts exist. |
| Validation framework complete | Nearly complete | Cross-reference validation added in 0015; final smoke tests remain before Milestone 1 closure. |
| Serialization framework complete | Nearly complete | Repository-backed loaders for current Data categories added in 0016; final smoke tests remain. |
| Repository layer complete | Nearly complete | Core Data catalog and repository-backed loaders added in 0016; typed category-specific models can be added as schemas stabilize. |
| Event system complete | Verified locally | Event system smoke test passed. |
| Dependency injection/service registry complete | Verified locally | Dependency-injection smoke test passed. |
| Version management complete | Substantially underway | Keep VERSION.md and ProjectManifest synchronized every update. |
| Project manifest reader complete | Substantially underway | Keep manifest schema stable and documented. |
| Repository health checker complete | Nearly complete | Cross-reference validation added in 0015; final milestone health report remains. |
| Shared utility library complete | In progress | Add utilities only when used by real Core code. |
| Unit-test framework established | Verified locally | Milestone 1 smoke-test project passed locally. |
| Runtime does not require .NET SDK | Active rule | Runtime must use compiled/runtime-ready artifacts from `Bin/`; GitHub Actions now verifies builds in the cloud. |
| Update package has exactly one top-level labeled folder | Active rule | Verify every ZIP before reporting complete. |
| Apply BAT runs from inside extracted update folder | Active rule | The BAT must automatically locate the project root. |

## Permanent Project Rules

- The latest uploaded repository ZIP is the source of truth after every applied update.
- If no repository ZIP is available, continue from the latest verified Code Update package only if it can be used as a real working baseline.
- If neither is available, ask the user to upload the latest repository ZIP.
- When the user says "start" or "continue", begin the next sequential Code Update without re-explaining the plan.
- All approved suggestions become implementation requirements unless the user explicitly defers or rejects them.
- The repository layout is frozen unless the user explicitly approves a structural change.
- `Data/` is the authoritative game database after import.
- `ImportedDatabases/` is archive/reference only.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Runtime must never require the .NET SDK. Development may use SDK tooling, but normal use must launch compiled/runtime-ready artifacts from `Bin/`.
- Every Code Update and Database Version ZIP must contain exactly one top-level folder named for the update, such as `CodeUpdate-0014-AI-Handoff-Standardization/`.
- Code Update ZIPs must be patch/update-only packages containing only changed/new update files plus required installer/manifest files; never package a rebuilt full repository unless the user explicitly asks for a full baseline.
- Users must be able to extract the ZIP directly into the main project folder without manually creating an update folder.
- The `Apply-*.bat` file must live inside that top-level update folder and run from there.
- The apply script must automatically locate the project root.
- Every update must update `README.md`, `CHANGELOG.md`, `VERSION.md`, `ProjectManifest.json`, and this `AI_CONTEXT.md`.
- Never claim an update package exists unless it has actually been created and verified.

## Handoff Maintenance Requirements

Every future Code Update must update this document using the same structure:

1. Update Current Repository Facts.
2. Update Milestone 1 Deliverable Status.
3. Update Milestone 1 Completion Checklist.
4. Add the new update to Update History.
5. Refresh Next Recommended Work.
6. Confirm permanent rules still reflect current user instructions.

Do not leave this document with stale update numbers, stale milestone estimates, or stale next-work items.

## Update History Summary

| Code Update | Summary |
|---|---|
| 0026 | Added Milestone 1 verification report, refreshed continuity docs, and prepared final freeze pending GitHub Actions. |
| 0025 | Completed active rebrand audit, removed obsolete duplicate MajorMud-named importer utility, and confirmed no gameplay changes. |
| 0024B | Corrected CoreStartupDiagnostics to use the verified Write(LogSeverity.Information, ...) logger API. No BAT installer or update ZIP was created per current user instruction. |
| 0017 | Added GitHub Actions cloud verification documentation, Bin runtime artifact expectations, and SDK-free runtime handoff catalog validation. |
| 0016 | Added repository-backed Core Data loaders, a Core Data catalog, loader health checks, and GitHub Actions cloud build verification. |
| 0015 | Added source-level Data cross-reference validation for rooms, items, monsters, shops, quests, loot tables, races, classes, and world data. |
| 0014 | Standardized `AI_CONTEXT.md` as the authoritative handoff document, added required maintenance rules, and documented the handoff format. |
| 0013 | Added duplicate model enforcement and repository health integration. |
| 0012 | Added runtime configuration, logging, service registry, repository contracts, JSON data handling, validation, and health checks. |
| 0011 | Wired application source shells toward Core and added runtime audit work. |
| 0010 | Added Core services and converted `AI_CONTEXT.md` into a living handoff document. |
| 0009 | Added Core audit/test foundations and data record contracts. |
| 0008 | Added governance documents and Core service foundations. |
| 0007 | Added Core contracts foundation. |


## Code Update 0017 Handoff Notes

Core Update 0017 formalizes the GitHub Actions verification recommendation and runtime artifact handoff expectations. It adds `.github/workflows/dotnet-build.yml`, documents private-repository cloud verification, adds `Bin/RuntimeArtifacts.json`, and adds an SDK-free `BinRuntimeHandoffCatalog` for future runtime artifact checks.

Normal users still do not need the .NET SDK locally. Build verification should come from GitHub Actions after the user pushes the update to their private GitHub repository.

## Code Update 0016 Handoff Notes

Core Update 0016 adds repository-backed loaders for the current canonical `Data/` JSON categories. The new loader projects flexible `CanonicalDataRecord` records into Core repositories while preserving raw JSON fields for future editor/server use. It also adds a Core Data catalog and integrates repository loader validation into Data JSON health checks.

This update also implements the user's approved verification suggestion: `.github/workflows/dotnet-build.yml` runs `dotnet restore` and `dotnet build` in GitHub Actions, allowing cloud build verification without requiring the user to install the .NET SDK locally.

## Code Update 0015 Handoff Notes

Core Update 0015 expands source-level Data JSON health checks from parse/root/duplicate-ID validation into cross-reference validation. It adds `DataCrossReferenceHealthCheck`, integrates it into `DataJsonHealthCheck`, and adds validation result merging support.

The new checker indexes current Data records and validates world room exits, room item references, room monster references, race class references, and future shop/quest/loot-table references. This moves the Milestone 1 validation framework closer to completion while preserving the no-SDK normal runtime rule.

## Code Update 0014 Handoff Notes

Core Update 0014 standardizes this file so a new ChatGPT window can immediately understand the project state without relying on conversation memory. It also adds documentation describing how `AI_CONTEXT.md` must be maintained in every future update.

This update is intentionally documentation/governance focused. It supports Milestone 1 by improving continuity, reducing stale context, and making future Core completion work safer.

## Next Recommended Work

1. Push CodeUpdate-0026 and confirm GitHub Actions restore/build/test passes.
2. After green Actions, record the Milestone 1 baseline freeze confirmation.
3. Begin Milestone 2 only after user approval.

## New Window Instruction

If you are a new ChatGPT window, do not ask the user to restate project history. Read this file and the required startup files, inspect the repository, then continue from the next recommended work unless the user gives a different instruction.

## CodeUpdate-0020 Note

GitHub Actions must use `.github/workflows/dotnet-build.yml` with `Reforged.*` project paths. If GitHub still displays `MajorMud.*` paths, replace the workflow file with `GitHubActions/dotnet-build.yml` and commit it.

## CodeUpdate-0023 Handoff

- Latest update adds a fresh GitHub Actions workflow for the current `Reforged.*` layout.
- Includes `Install-GitHub-Actions-Workflow.bat` at repository root.
- The batch file copies `GitHubActions/dotnet-build.yml` to `.github/workflows/dotnet-build.yml`.
- Continue to exclude protected user/runtime data directories from update packages unless explicitly requested.

## CodeUpdate-0024 Handoff

Milestone 1 completion has started. CodeUpdate-0024 is verification-only and avoids new gameplay systems.

Completed in this update:

- Added Core event system contracts and `InMemoryEventBus`.
- Added `CoreStartupDiagnostics` and `StartupDiagnosticReport`.
- Added `Reforged.Core.Tests` smoke-test project.
- Updated GitHub Actions to build and run smoke tests.

Important validation note:

- The local packaging container did not include the .NET SDK. Treat GitHub Actions as the authoritative build/test result for this update.

Next update:

- CodeUpdate-0026 - Milestone 1 Finalization.
- Generate Milestone 1 verification report.
- Update continuity docs and prepare Milestone 2 roadmap only after verification passes.


## CodeUpdate-0024B Handoff

CodeUpdate-0024B is a corrective build-fix patch for CodeUpdate-0024. It fixes the CoreStartupDiagnostics logger API mismatch by replacing the invalid concrete Info(...) call with the verified existing Write(LogSeverity.Information, ...) method on InMemoryReforgedLogger.

This patch does not add gameplay features. No BAT installer or update ZIP was created per current user instruction. Run local dotnet restore, dotnet build, and smoke tests when the SDK is available; GitHub Actions remains the official validation gate after commit and push.

## CodeUpdate-0025 Handoff

CodeUpdate-0025 completes the active rebrand audit for Milestone 1 closure. Active source projects, assemblies, namespaces, and workflow paths use Reforged.*. The obsolete duplicate importer utility Tools/Importer/MajorMudDatDecoder.py was removed because Tools/Importer/ReforgedDatDecoder.py is the active equivalent used by current importer scripts.

This patch does not add gameplay features. Codex writes only to the local workspace; the user controls the GitHub push. GitHub Actions on github.com/ShisenShi1017/MajorMud-Reforged-V1.0 remains the official validation gate after push.

## CodeUpdate-0025 Verification

Local .NET 8.0.422 verification passed for CodeUpdate-0025. Restore/build passed for Core, Server, Importer, Editor, Launcher, Client, and the Core smoke-test project. The smoke-test run reported all Milestone 1 core smoke tests passed. GitHub Actions on github.com/ShisenShi1017/MajorMud-Reforged-V1.0 remains the official validation gate after the user pushes.

## CodeUpdate-0026 Handoff

CodeUpdate-0026 prepares the Milestone 1 Core Framework baseline for final freeze. It adds Reports/CodeUpdates/CodeUpdate-0026/Milestone1VerificationReport.md, updates continuity documents, and records local .NET 8.0.422 restore/build/smoke-test verification.

This patch does not add gameplay features. Codex writes only to the local workspace; the user controls the GitHub push. Milestone 1 should be marked frozen only after CodeUpdate-0026 GitHub Actions is green on github.com/ShisenShi1017/MajorMud-Reforged-V1.0.

Next after green Actions: record the freeze confirmation, then begin Milestone 2 planning/implementation only with user approval.

## CodeUpdate-0026 Verification

Local CodeUpdate-0026 verification passed on .NET SDK 8.0.422 after finalization edits. Restore/build passed for Core, Server, Importer, Editor, Launcher, Client, and the Core smoke-test project. The smoke-test run reported all Milestone 1 core smoke tests passed. GitHub Actions remains pending until the user pushes CodeUpdate-0026.
