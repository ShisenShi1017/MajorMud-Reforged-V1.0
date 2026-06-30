# Reforged

> A modern standalone reimplementation of Reforged designed for long-term maintainability, live editing, and future cross-platform support.

## Current Project State

- **Current Code Update:** `CodeUpdate-0041 - Classic Parity Audit Foundation`
- **Current Database Version:** `DatabaseVersion-0009 - Server-Ready Data Baseline`
- **Active Milestone:** `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- **Current Priority:** Push CodeUpdate-0041, verify GitHub Actions green, then approve one narrow Classic parity parser/comparison or server-readiness scope.

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
8. `DEVELOPMENT_PRINCIPLES.md`
9. Relevant source, test, and application documentation for the requested change

## CodeUpdate-0041 Summary

CodeUpdate-0041 adds the first read-only Classic parity audit foundation. Server bootstrap now inventories canonical `Data/` and imported reference material, prints broad Classic category status, and keeps gameplay disabled.

This does not parse proprietary legacy binary formats yet, does not rewrite `Data/`, and does not claim exact MajorMUD parity.

## CodeUpdate-0040 Summary

CodeUpdate-0040 preserves the user-provided WCC-labeled WBB archive under `ImportedDatabases/WCCLabeledWBB/` as reference/import material only.

All WCC data and WCC-labeled data is reference-only unless the user explicitly approves a reviewed import or reconciliation update.

## CodeUpdate-0039 Summary

CodeUpdate-0039 improves the non-gameplay Server bootstrap by exposing a compact host-tool readiness summary. Startup output now states whether the repo is ready for host/editor tooling while keeping gameplay status explicitly disabled.

CodeUpdate-0038 GitHub Actions is green by user report.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0038 Summary

CodeUpdate-0038 improves the non-gameplay Server bootstrap by exposing required-directory status records and counts in the bootstrap report and startup output. This helps future host/editor tooling inspect repository readiness without parsing validation errors.

CodeUpdate-0038 GitHub Actions is green by user report.

CodeUpdate-0037 GitHub Actions is green by user report.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0037 Summary

CodeUpdate-0037 is a documentation-only update that combines the user-provided CodeUpdate-0036B continuity cleanup package with the user-provided CodeUpdate-0037 Software Design Specification package. It repairs stale continuity after the direct-GitHub CodeUpdate-0036 documentation commits and adds draft `Docs/SDS/` planning records for future systems.

CodeUpdate-0037 GitHub Actions is green by user report.

No source-code behavior, data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0036 Summary

CodeUpdate-0036 adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits.

No source-code behavior, data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0035 Summary

CodeUpdate-0035 improves the non-gameplay Server bootstrap by exposing validation issue counts and printing a startup validation summary by severity. It keeps the work on the pre-website host/server track.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0034 Summary

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. OmniVerse may help identify possible mismatches in Reforged data, but it must not automatically overwrite canonical `Data/` or archive `ImportedDatabases/`.

CodeUpdate-0034 GitHub Actions is green by user report.

No data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0033 Summary

CodeUpdate-0033 improves the non-gameplay Server bootstrap by making exit-code behavior an explicit report contract. Server startup returns the report exit code, and smoke tests verify success exposes exit code 0 while failure exposes exit code 1.

CodeUpdate-0033 GitHub Actions is green by user report.

No gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0032 Summary

CodeUpdate-0032 improves the non-gameplay Server bootstrap by capturing ProjectManifest identity fields and printing them during startup diagnostics: project, CodeUpdate, database version, and milestone. It also adds smoke-test coverage for those captured fields.

Local .NET 8.0.422 restore/build/smoke verification passed. GitHub Actions is green by user report.

No gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

## CodeUpdate-0031A Summary

CodeUpdate-0031A is a corrective build-fix patch for CodeUpdate-0031. GitHub Actions reported a C# syntax error in `Tests/Source/Reforged.Core.Tests/Program.cs` at the configuration smoke-test JSON line. This patch fixes the test JSON string escaping so the test project can compile.

No gameplay features, combat, character creation, world simulation, networking expansion, or live editor implementation are added.

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

The project goal is to build a standalone Reforged server, shared Core framework, native importer, live editor, launcher, Windows client, Android client, gameplay systems, and plugin framework.

Reforged must not depend on Microsoft Access, OLEDB, the original legacy executable, or the original runtime files during normal server operation. Original legacy files are import/reference material only.

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

The standalone server must load from `Data/` only. After successful conversion, imported databases and original runtime files are kept only for audit, verification, troubleshooting, and future re-import work.

All WCC data, including WCC default installs, WCC-labeled WBB material, WSGSERVE-related files, and WCC-derived archives, must be treated as reference-only unless the user explicitly states otherwise for a reviewed update.

OmniVerse may be used as a read-only comparison source for future audits. Website/API data can support a proposed correction, but corrections must be reviewed and approved before changing `Data/`.

## Server Mode Direction

The first real Server target is the Classic baseline. The Server should load and run Classic data first.

Reforged mode, Custom mode, AI systems, alternate gameplay rules, and advanced behavior are not approved yet. Server architecture should stay mode-ready, but only simple extension points needed for future mode/profile loading should be added before the Classic baseline is working.

`Data/` defines world/content. Server defines and enforces engine behavior. The Editor should eventually edit data safely.

Future intended evolution:

1. Classic server baseline.
2. Mode/profile loader.
3. Reforged mode as Classic-derived data/rules overrides.
4. Custom/editor-created worlds.
5. AI systems and advanced behavior only after explicit approval.

## Engine Architecture Direction

Classic is the first target, but the Server is an engine, not a Classic-only server.

Favor reusable engine systems over feature-specific systems. Keep the engine generic whenever practical. Avoid naming or designing systems around Classic unless the behavior is truly Classic-specific.

When designing new Server features, ask whether the feature can be data-driven, reused by future modes, exposed to the Editor later, and implemented without unnecessarily tying the engine to Classic.

Classic, Reforged, custom worlds, future expansions, and additional rule sets should not require separate server codebases. Favor extensibility over shortcuts, but avoid premature complexity.

## WCC Default Install Reference

The user-provided default WCC install archive is preserved under `ImportedDatabases/WCCDefaultInstall/` as reference/import material.

This source is not live `Data/`, not a runtime dependency, and not an approval to rewrite canonical Reforged data. Any conversion or reconciliation must be a separate reviewed update.

## Code Update Rules

Code Updates are for repository, Core, server, editor, importer, launcher, client, tooling, and documentation changes.

Every Code Update must inspect GitHub truth first, verify current state, make the smallest safe approved change, update continuity documents, preserve `Saves/` and `ImportedDatabases/`, and record what was verified.

For ChatGPT ZIP updates, every package must be patch-only, include an `Apply-CodeUpdate-XXXX.bat`, include `UpdateManifest.json`, run from its own extracted folder, require no manual path entry, and be verified before announcement.

## Executable Policy

Do not claim an executable exists unless it has actually been built.

Future executable locations:

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

Milestone 1 established shared models, validation types, repository contracts, configuration contracts, logging contracts, serialization contracts, event system contracts, version metadata, repository health reporting, dependency injection/service registry, smoke-test foundation, and application references to Core.

## Milestone 2 - Engine Modernization / Host & World Editor Planning

Status: active.

CodeUpdate-0029 started Milestone 2 implementation with startup-only Server diagnostics. CodeUpdate-0030 recorded permanent handoff and source-of-truth rules. CodeUpdate-0031 added the first non-gameplay Server bootstrap foundation around the existing diagnostics path. CodeUpdate-0031A corrects the 0031 smoke-test compile failure. CodeUpdate-0032 adds manifest identity output. CodeUpdate-0033 adds an explicit bootstrap exit-code contract. CodeUpdate-0034 adds the OmniVerse read-only reference audit policy. CodeUpdate-0035 adds bootstrap validation summary counts. CodeUpdate-0036 adds knowledge-base/design-decision documentation. CodeUpdate-0037 adds draft Software Design Specification planning records and repairs stale continuity. CodeUpdate-0038 adds required-directory status reporting to Server bootstrap. CodeUpdate-0039 adds host-tool readiness summary output. CodeUpdate-0040 preserves WCC-labeled WBB material as reference/import material only. CodeUpdate-0041 adds the read-only Classic parity audit foundation.

Next safe work after CodeUpdate-0041 is pushed and GitHub Actions is green:

1. Approve one narrow field-level Classic parser/comparison or server-readiness scope.
2. Keep work narrow and verifiable.
3. Do not add gameplay systems without explicit user approval.

## Long-Term Roadmap

1. Maintain the frozen Core Framework baseline.
2. Continue engine modernization and host/server startup work.
3. Build native Importer.
4. Build standalone Server runtime from `Data/`.
5. Build server data loading and validation.
6. Build live Editor through server/admin API.
7. Build Launcher and update manager.
8. Build companion client-style Windows Client.
9. Build Android Client.
10. Complete gameplay systems.
11. Add plugin framework.
12. Prepare Version 1.0.

## Important Reminder

This is a professional software project with versioned Code Updates and Database Versions. Accuracy matters more than speed. Never overstate what has been implemented.
