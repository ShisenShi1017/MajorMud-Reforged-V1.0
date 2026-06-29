# Reforged


## CodeUpdate-0019 Rebrand Note

The active source, assembly, and project naming pattern is now `Reforged.*`. GitHub Actions builds the renamed projects, and normal runtime still does not require the .NET SDK on the user machine.
> A modern standalone reimplementation of Reforged designed for long-term maintainability, live editing, and future cross-platform support.

---

## Current Project State

**Current Code Update:** `CodeUpdate-0029 - Host Server Startup Diagnostics`

**Current Database Version:** `DatabaseVersion-0009 - Server-Ready Data Baseline`

**Active Milestone:** `Milestone 2 - Engine Modernization / Host & World Editor Planning`

**Current Priority:** Push CodeUpdate-0029, verify GitHub Actions, then approve the next small Milestone 2 host/server scope.

This README is the required starting point for every new ChatGPT session and every developer handoff.

---

## CodeUpdate-0028 Summary

CodeUpdate-0028 records the user-reported green GitHub Actions result for CodeUpdate-0027 and creates the first Milestone 2 scope plan. It recommends CodeUpdate-0029 as a small, verifiable host/server startup and runtime diagnostics foundation. It does not add gameplay features or implementation code.

## CodeUpdate-0029 Summary

CodeUpdate-0029 starts Milestone 2 implementation by adding startup-only Server diagnostics. The Server path now verifies identity, configuration defaults, service registry wiring, event bus behavior, and existing Core startup diagnostics without starting gameplay systems, combat, character creation, world simulation, editor work, or networking expansion.

## CodeUpdate-0027 Summary

CodeUpdate-0027 freezes the Milestone 1 Core Framework baseline after the user reported CodeUpdate-0026B GitHub Actions green. This update records the official gate result, marks Milestone 1 complete/frozen, and prepares Milestone 2 planning. It does not add gameplay features.

## CodeUpdate-0026B Summary

CodeUpdate-0026B cleans the Milestone 1 finalization candidate by removing generated source-project build outputs, repairing targeted ignore rules, and fixing visible documentation encoding damage. It does not add gameplay features. Milestone 1 should be frozen only after the user pushes this patch and GitHub Actions is green.

## CodeUpdate-0026 Summary

CodeUpdate-0026 prepares Milestone 1 for final freeze by adding a verification report, refreshing continuity documents, and recording local restore/build/smoke-test verification. It does not add gameplay features. The Milestone 1 baseline should be marked frozen only after the user pushes this patch and GitHub Actions is green.

## CodeUpdate-0025 Summary

CodeUpdate-0025 completes the active rebrand audit for Milestone 1 closure. Active source projects, assemblies, namespaces, and GitHub Actions workflow paths use Reforged.*. The obsolete duplicate importer utility Tools/Importer/MajorMudDatDecoder.py was removed because Tools/Importer/ReforgedDatDecoder.py is the active tool. No gameplay features were added, and Codex did not create a BAT installer, update ZIP, or GitHub push.

## CodeUpdate-0024B Summary

CodeUpdate-0024B is a corrective Milestone 1 build-fix patch. It repairs the CoreStartupDiagnostics logger API mismatch by replacing the invalid concrete Info(...) call with the verified existing Write(LogSeverity.Information, ...) logging API. It does not add gameplay features. No BAT installer or update ZIP was created per current user instruction.

## CodeUpdate-0015 Summary

Core Update 0015 expands Data JSON validation from individual file checks into cross-reference validation. The source-level Core health checks now index rooms, items, monsters, classes, races, shops, quests, loot tables, and world room data, then report missing references such as room exits pointing at missing rooms, rooms containing missing item or monster IDs, races using missing classes, and future shop/quest/loot-table references targeting missing records.


## CodeUpdate-0014 Summary

Core Update 0014 standardizes `AI_CONTEXT.md` as the authoritative handoff document for every new ChatGPT window and development continuation. It adds a required startup order, current repository facts, a living Milestone 1 status table, a completion checklist, update history, next recommended work, and maintenance rules requiring every future Code Update to refresh the handoff document.

## CodeUpdate-0013 Summary

Core Update 0013 adds automated duplicate model enforcement. Application projects must not redefine shared model-like types that already belong to Core. Repository health checks now include duplicate model findings so the Milestone 1 no-duplicated-models rule can be verified instead of tracked only by manual review.

## Project Vision

Reforged is a completely standalone Reforged platform.

The project goal is to build:

- A standalone Reforged server.
- A shared Core framework used by every application.
- A native importer that converts original legacy data into Reforged data.
- A live editor that works through the server safely.
- A launcher that eventually replaces update/apply batch workflows.
- A future companion client-style Windows client.
- A future Android client.

Reforged must not depend on:

- Microsoft Access.
- OLEDB.
- The original legacy executable.
- The original runtime files during normal server operation.

Original legacy files are import/reference material only.

---

## Source of Truth

The current repository is the only code source of truth.

Every update must begin by inspecting the latest repository snapshot before making changes.

Do not recreate the project from scratch.

Do not assume older folder layouts still exist.

Do not claim an update, executable, patch, feature, or database conversion exists unless it has actually been created and verified.

---

## Required Repository Layout

The current repository layout is considered frozen unless the user explicitly approves a future restructure.

```text
Apps/
    Launcher/
    Server/
    Editor/
    Importer/
    Client/

Core/

Data/

ImportedDatabases/

Docs/

Reports/

Saves/

Tools/

Updates/

Backups/

Bin/
    Windows/
        Launcher/
        Server/
        Editor/
        Importer/
        Client/
    Android/
        Client/
```

---

## Data Philosophy

`ImportedDatabases/` contains original/reference source data.

`Data/` is the real Reforged game database.

The standalone server must load from `Data/` only.

After successful conversion, imported databases and original runtime files are kept only for audit, verification, troubleshooting, and future re-import work.

The project must continue moving toward simple editable data that allows every part of the game to be edited safely.

---

## Database Version Rules

Database Versions are for data/import/schema work.

Every Database Version must:

- Run from its own extracted folder.
- Include an `Apply-DatabaseVersion-XXXX.bat` file.
- Include a manifest.
- Include release notes.
- Update docs.
- Update audit reports.
- Preserve `Saves/`.
- Preserve `ImportedDatabases/` unless the user explicitly approves otherwise.
- Detect duplicate IDs where applicable.
- Detect missing references where applicable.
- Clearly state what is verified and what remains unknown.

No database version may claim full import or decode completion unless every source table/file/field/record involved has been accounted for.

---

## Code Update Rules

Code Updates are for repository, Core, server, editor, importer, launcher, client, and tooling changes.

Every Code Update must:

- Inspect the latest repository first.
- Verify the current milestone.
- Verify the current Code Update number from repository files and user direction.
- Verify the current Database Version.
- Verify the current Project Version when present.
- Verify the update follows the project vision.
- Package only the changed/new update files, not a rebuilt copy of the entire repository.
- Include an `Apply-CodeUpdate-XXXX.bat` file.
- Run from its own extracted folder.
- Require no manual path entry.
- Include `UpdateManifest.json`.
- Update `README.md`.
- Update `CHANGELOG.md`.
- Update `VERSION.md`.
- Update `ProjectManifest.json`.
- Update docs and reports.
- Preserve `Saves/`.
- Preserve `ImportedDatabases/` unless explicitly approved.
- Verify the actual ZIP file exists before announcing it.

Until the launcher/updater exists, batch files remain the temporary compatibility apply mechanism. The product direction is to move away from PowerShell/script-driven operation and toward compiled applications with dedicated executable folders.

---

## Executable Policy

Do not claim an executable exists unless it has actually been built.

Every future executable must live in its own folder:

```text
Bin/Windows/Launcher/
Bin/Windows/Server/
Bin/Windows/Editor/
Bin/Windows/Importer/
Bin/Windows/Client/
Bin/Android/Client/
```

The user does not want to install SDKs or developer runtimes just to run finished tools. Long term, releases should include runnable builds rather than requiring local developer tool installation.

---

## Current Milestone: Milestone 1 - Core Framework

The active milestone is Core.

Milestone 1 should create the shared foundation used by all applications.

Core should contain:

- Shared models.
- Validation types.
- Repository contracts.
- Source/provenance tracking.
- Configuration contracts.
- Logging contracts.
- Serialization contracts.
- Event system contracts.
- Version metadata.
- Repository health reporting.

Do not start gameplay systems until the Core baseline is strong enough for the server, importer, editor, launcher, and client to share the same definitions.

---

## Application Architecture

All applications should depend on Core.

Applications should not directly depend on each other.

```text
Apps
  |
  v
Core
  |
  v
Data Repository
  |
  v
Server
  |
  +-- Editor API
  +-- Client Sessions
  +-- Launcher / Admin Tools
```

The editor should eventually edit live server data through a server/admin API rather than editing runtime files directly while the server is running.

---

## Current Next Task

Create **Code Update 0006** if not already applied, continuing Milestone 1 with real repository changes.

The next Core work should focus on actual source files and contracts for:

- Core event system.
- Core service registry/contracts.
- Core validation pipeline.
- Data repository loading contracts.
- Source/provenance tracking.
- Server-ready world model interfaces.

Repository reorganization is complete. Future work should focus on functionality, not more folder cleanup, unless the user explicitly asks.

---

## ChatGPT Continuation Rules

Every new ChatGPT session must:

1. Read this README first.
2. Inspect the uploaded/current repository before changing anything.
3. Treat the uploaded/current repository as the only code source of truth.
4. Use the current chat/project vision summarized here.
5. Continue the current milestone unless the user changes priority.
6. Create targeted updates only.
7. Ensure updates run from their own extracted folder.
8. Keep docs synchronized with every update.
9. Preserve `Saves/` and `ImportedDatabases/`.
10. Verify that the actual update ZIP exists before linking it.

---

## Current Long-Term Roadmap

1. Complete Core Framework.
2. Build native Importer.
3. Build standalone Server bootstrap.
4. Build server data loading and validation.
5. Build live Editor through server/admin API.
6. Build Launcher and update manager.
7. Build companion client-style Windows Client.
8. Build Android Client.

---

## Important Reminder

This is a professional software project with versioned Code Updates and Database Versions.

Accuracy matters more than speed.

Never overstate what has been implemented.

## CodeUpdate-0007 - Core Contracts Foundation

Milestone 1 continues with Core contracts for events, services, validation pipelines, repositories, provenance, runtime state, and world model interfaces.

Runtime rule: Reforged must not require the .NET SDK to run. Normal users must launch compiled executables or runtime-ready artifacts from `Bin/`; SDK launch/build commands are not valid runtime paths.

Update rule: each Code Update must be executable from its own extracted folder and apply into the main project folder through its included `Apply-*.bat` script.
## Code Update 0008 - Governance and Core Services

Core Update 0008 continues Milestone 1 by adding project-governance documents and additional Core services. New sessions should begin with `AI_CONTEXT.md`, then `Docs/Architecture/PROJECT_ROADMAP.md`.

Permanent rules now include: runtime must not require the .NET SDK, update packages must run from their own extracted folder, and all approved suggestions become required implementation work unless explicitly deferred or rejected.

## Current Development Status

- Current Code Update: 0009
- Current Milestone: Milestone 1 - Core Framework
- Current focus: Core audit contracts, shared data record model, repository catalog, and dependency-free test foundation.
- Runtime rule: normal project runtime must not require the .NET SDK.
- Update rule: every Code Update must run from its own extracted folder and apply to the main project folder.
- Suggestion rule: approved suggestions become implementation requirements unless explicitly deferred or rejected.

Start new development sessions by reading `AI_CONTEXT.md`, then `Docs/Architecture/PROJECT_ROADMAP.md`.


## CodeUpdate-0010 Notes

Core Update 0010 continues Milestone 1 and adds a stronger living handoff process.

Highlights:

- `AI_CONTEXT.md` is now a living handoff document and must be updated every Code Update.
- The handoff includes the completed/substantially-underway table and the Milestone 1 completion checklist.
- Update ZIPs must contain exactly one labeled top-level folder so users can extract directly into the main project folder.
- The `Apply-*.bat` script must run from inside that extracted update folder and automatically locate the project root.
- Core configuration now supports typed values, JSON file loading, environment loading, and value source tracking.
- Core logging now includes sink contracts, composite logging, and an in-memory sink.
- Core validation now includes merge helpers and a reusable required-string validator.
- Core serialization now includes a shared JSON file store.
- Core repository work now includes an in-memory data repository for staging/testing.
- Repository health work now includes update package layout validation.


## Core Update 0011

Core Update 0011 continues Milestone 1 by wiring all application source shells to Core while preserving the no-SDK runtime rule. The Server, Importer, Editor, Launcher, and Client now have source-level project shells under `Apps/Source/` with explicit Core project references. Core also gains application identity contracts, an application Core-reference audit, and runtime artifact validation models for future `Bin/` handoff checks.


## Code Update 0012 Notes

Code Update 0012 continues Milestone 1 by adding shared source-level Core services for runtime configuration, logging, service registration, repository staging, JSON data file loading, data-record validation, Data JSON health checks, and application Core-reference health checks. These additions move the project closer to closing the Core Framework milestone while preserving the rule that normal runtime must not require the .NET SDK.

## Current Code Update

Current Code Update: **0027 - Milestone 1 Baseline Freeze**

Milestone 1 is frozen after the user reported CodeUpdate-0026B GitHub Actions green. CodeUpdate-0027 records the freeze and prepares Milestone 2 planning. Normal runtime still does not require the .NET SDK on the user machine.



## GitHub Actions Build Verification

This repository includes `.github/workflows/dotnet-build.yml` so GitHub can restore and build the Core and application source projects in the cloud. This is the recommended verification path when the user does not want to install the .NET SDK locally. The repository may remain private. See `Docs/Architecture/GITHUB_ACTIONS_VERIFICATION.md`.

`Bin/RuntimeArtifacts.json` documents the future runtime-ready executable locations. Normal users should launch compiled artifacts from `Bin/` and should not need the .NET SDK for normal runtime.

## CodeUpdate-0020 Summary

CodeUpdate-0020 repairs GitHub Actions after the Reforged source rebrand. The workflow now restores and builds `Reforged.*` project paths and includes a guard that fails early if stale `MajorMud.*` project paths remain under `.github/workflows`.

## GitHub Actions setup on Windows

This repository includes a helper batch file for installing the cloud build workflow:

```bat
Install-GitHub-Actions-Workflow.bat
```

Run it from the repository root after extracting the update. It copies `GitHubActions\dotnet-build.yml` into `.github\workflows\dotnet-build.yml`. After committing and pushing, GitHub Actions will restore and build the `Reforged.*` source projects in the cloud.

### CodeUpdate-0024 Verification Track

Milestone 1 closure completed in CodeUpdate-0027 after dependency injection, configuration loading, repository health, event-system behavior, startup diagnostics, smoke tests, and GitHub Actions validation were recorded.

GitHub Actions is the authoritative build gate and now restores, builds, and runs the Core smoke-test project.


### CodeUpdate-0024B Corrective Patch

CodeUpdate-0024B repairs the CodeUpdate-0024 build failure in CoreStartupDiagnostics by using the existing in-memory logger Write(LogSeverity.Information, ...) API. This direct repository patch updates continuity documents and does not create a BAT installer or update ZIP per current user instruction. GitHub Actions remains the official validation gate after the patch is committed and pushed.
