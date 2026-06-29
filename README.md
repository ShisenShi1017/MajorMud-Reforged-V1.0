# Reforged

> A modern standalone reimplementation of Reforged designed for long-term maintainability, live editing, and future cross-platform support.

## Current Project State

- **Current Code Update:** `CodeUpdate-0030 - Truth Documentation Cleanup and Agent Handoff Rules`
- **Current Database Version:** `DatabaseVersion-0009 - Server-Ready Data Baseline`
- **Active Milestone:** `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- **Current Priority:** Apply/push CodeUpdate-0030, verify GitHub Actions green, then approve the next small Milestone 2 host/server scope.

This README is a required starting point for every new ChatGPT, Codex, or developer handoff session.

## Required Startup Order

Every session must read these files before changing the repository:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/Architecture/PROJECT_ROADMAP.md`
4. `README.md`
5. `VERSION.md`
6. `CHANGELOG.md`
7. `ProjectManifest.json`
8. Relevant source, test, and application documentation for the requested change

## CodeUpdate-0030 Summary

CodeUpdate-0030 is a documentation/governance cleanup. It adds `AGENTS.md`, records the Codex-vs-ChatGPT workflow rules, makes GitHub the authoritative source of truth, removes stale continuity language, and records the permanent rule to never guess when creating or reporting updates.

This update does not add gameplay features or source implementation code.

## Current Source of Truth

The GitHub repository is the authoritative source of truth:

`ShisenShi1017/MajorMud-Reforged-V1.0`

Rules:

- Always inspect GitHub truth before planning or changing work.
- Do not rely on old ZIP files, previous patches, or conversation memory when they conflict with GitHub.
- Do not recreate the project from scratch.
- Do not assume old folder layouts still exist.
- Do not claim an update, executable, package, patch, feature, database conversion, build result, or GitHub Actions result exists unless it has actually been verified.

## Work Mode Rules

### Codex mode

Codex is used when working in a local repository coding environment.

- Codex updates the local repository only.
- Codex must wait after each update until the user pushes and states the current push is green in GitHub Actions.
- Codex must not continue to the next Code Update until that green status is confirmed or the user explicitly overrides the gate.
- Codex must update continuity documents before ending the update.

### ChatGPT mode

ChatGPT is used when working in a normal chat window without direct local repository access.

- ChatGPT does not directly modify the user's local repository.
- ChatGPT continues through ZIP upload / ZIP patch workflow unless the user explicitly chooses direct GitHub edits.
- ChatGPT-created update ZIPs must be patch-only, contain exactly one top-level folder, include an apply BAT, include a manifest, and be verified before delivery.

## Never Guess Rule

Never guess when creating, fixing, reviewing, or reporting an update.

- Verify method names before changing code.
- Verify file paths before referencing them.
- Verify package contents before linking ZIPs.
- Verify current update, milestone, and database version from repository files.
- Verify build/test status before claiming success.
- If something cannot be verified, state that clearly instead of filling the gap with assumptions.

## Project Vision

Reforged is a standalone game engine and toolset inspired by the original legacy MUD.

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

## Required Repository Layout

The current repository layout is frozen unless the user explicitly approves a future restructure.

```text
Apps/
    Launcher/
    Server/
    Editor/
    Importer/
    Client/
    Source/

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

## Data Philosophy

`ImportedDatabases/` contains original/reference source data.

`Data/` is the real Reforged game database.

The standalone server must load from `Data/` only.

After successful conversion, imported databases and original runtime files are kept only for audit, verification, troubleshooting, and future re-import work.

## Code Update Rules

Code Updates are for repository, Core, server, editor, importer, launcher, client, tooling, and documentation changes.

Every Code Update must:

- Inspect the latest GitHub repository truth first.
- Verify the current milestone.
- Verify the current Code Update number from repository files and user direction.
- Verify the current Database Version.
- Verify the current Project Version when present.
- Verify the update follows the project vision.
- Make the smallest safe change that satisfies the approved scope.
- Update all relevant continuity documents.
- Preserve `Saves/`.
- Preserve `ImportedDatabases/` unless explicitly approved.
- Record what was verified and what remains unverified.

For ChatGPT ZIP updates, every Code Update package must:

- Package only the changed/new update files, not a rebuilt copy of the entire repository.
- Include an `Apply-CodeUpdate-XXXX.bat` file.
- Run from its own extracted folder.
- Require no manual path entry.
- Include `UpdateManifest.json`.
- Verify the actual ZIP file exists and contains exactly one top-level folder before announcing it.

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

No database version may claim full import or decode completion unless every source table, file, field, and record involved has been accounted for.

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

## Milestone 1 - Core Framework

Status: complete/frozen after the user reported green GitHub Actions for the Milestone 1 freeze sequence.

Milestone 1 established:

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
- Dependency injection/service registry.
- Smoke-test foundation.
- Application references to Core.

## Milestone 2 - Engine Modernization / Host & World Editor Planning

Status: active.

CodeUpdate-0029 started Milestone 2 implementation with startup-only Server diagnostics. The Server diagnostics path verifies identity, configuration defaults, service registry wiring, event bus behavior, and existing Core startup diagnostics without starting gameplay systems, combat, character creation, world simulation, editor work, or networking expansion.

CodeUpdate-0030 records the permanent handoff and source-of-truth rules before further Milestone 2 implementation.

Next safe work after CodeUpdate-0030 is pushed and GitHub Actions is green:

1. Approve the next small Milestone 2 host/server scope.
2. Keep work narrow and verifiable.
3. Do not add gameplay systems without explicit user approval.

## Long-Term Roadmap

1. Maintain the frozen Core Framework baseline.
2. Continue engine modernization and host/server startup work.
3. Build native Importer.
4. Build standalone Server bootstrap.
5. Build server data loading and validation.
6. Build live Editor through server/admin API.
7. Build Launcher and update manager.
8. Build companion client-style Windows Client.
9. Build Android Client.
10. Complete gameplay systems.
11. Add plugin framework.
12. Prepare Version 1.0.

## Important Reminder

This is a professional software project with versioned Code Updates and Database Versions.

Accuracy matters more than speed. Never overstate what has been implemented.
