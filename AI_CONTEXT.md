# AI Context for MajorMud Reforged

Read this file first in every new ChatGPT window or development session. This is the living handoff document and must be updated with every Code Update.

MajorMud Reforged is a standalone game engine and toolset inspired by the original MajorMUD. The original data is an import source only. After import, `Data/` is the live editable game database and `ImportedDatabases/` is archive/reference only.

## Start Here

1. Read this file completely.
2. Read `Docs/Architecture/PROJECT_ROADMAP.md`.
3. Read `README.md`.
4. Read `VERSION.md`.
5. Read `CHANGELOG.md`.
6. Read `ProjectManifest.json`.
7. Inspect the repository before making changes.

## Current Handoff State

- Current Milestone: Milestone 1 - Core Framework
- Current Code Update: 0010
- Current Database Version: 0009
- Last Update Name: CodeUpdate-0010-Core-Services-And-Handoff
- Milestone Estimate: 65-70% complete
- Next Work Mode: Continue Milestone 1 until completion criteria are satisfied.

## Completed or Substantially Underway

| Deliverable | Status | Notes |
|---|---:|---|
| Core configuration system | In progress | Shared provider now supports typed access, JSON file loading, environment loading, and entry provenance. |
| Core logging system | In progress | Shared logger contracts, file logger, composite logger, sink contract, and memory sink exist. |
| Validation framework | In progress | Pipeline, validators, results, severities, merge helpers, and reusable required-string validator exist. |
| Serialization framework | In progress | Shared JSON serializer and JSON file store exist. |
| Repository interfaces | Substantially underway | Read-only, editable, catalog, world repository, and in-memory staging repository contracts/implementation exist. |
| Shared data models | Substantially underway | Shared `IReforgedDataRecord` and extensible `GameEntity` exist to prevent duplicate application models. |
| Event system | Substantially underway | Event contracts, base events, publisher/subscriber contracts, and in-memory event bus exist. |
| Dependency injection/service registry | In progress | Service contracts and lightweight registry exist. |
| Version management | Substantially underway | Version info and version manager exist. |
| Project manifest reader | In progress | Shared manifest model and reader exist and now include update/handoff policy fields. |
| Repository health checker | In progress | Repository health report, issue model, checker, audits, and update package layout check exist. |
| Shared utility library | In progress | Path-safety utility exists; more utilities are expected as features require them. |
| Unit-test framework | Started | Dependency-free assertion and test result foundation exists. |
| Applications using Core | Not complete | App folders exist as placeholders; compile-level app projects/references still need to be created or wired. |

## Milestone 1 Completion Checklist

| Requirement | Status |
|---|---:|
| No duplicated models across applications | In progress |
| Every application references Core | Not complete |
| Core has no dependencies on application projects | In progress |
| Core configuration fully functional | In progress |
| Core logging complete | In progress |
| Validation framework complete | In progress |
| Serialization framework complete | In progress |
| Repository layer complete | In progress |
| Event system complete | Substantially underway |
| Dependency injection/service registry complete | In progress |
| Version management complete | Substantially underway |
| Project manifest reader complete | In progress |
| Repository health checker complete | In progress |
| Shared utility library complete | In progress |
| Unit-test framework established | Started |
| Runtime does not require .NET SDK | Active rule |
| Update package has exactly one top-level labeled folder | Active rule |
| Apply BAT runs from inside extracted update folder | Active rule |

## Permanent Rules

- The latest uploaded repository is the source of truth.
- If no repository ZIP is available, continue from the latest verified Code Update package only if it can be used as a real working baseline.
- If neither is available, ask the user to upload the latest repository ZIP.
- When the user says "start" or "continue", begin the next sequential Code Update without re-explaining the plan.
- All approved suggestions become implementation requirements unless the user explicitly defers or rejects them.
- The repository layout is frozen unless the user explicitly approves a structural change.
- `Data/` is the authoritative game database after import.
- `ImportedDatabases/` is archive/reference only.
- Never overwrite `Saves/` or `ImportedDatabases/` unless explicitly approved.
- Runtime must never require the .NET SDK. Development may use SDK tooling, but normal use must launch compiled/runtime-ready artifacts from `Bin/`.
- Every Code Update and Database Version ZIP must contain exactly one top-level folder named for the update, such as `CodeUpdate-0010-Core-Services-And-Handoff/`.
- Users must be able to extract the ZIP directly into the main project folder without manually creating an update folder.
- The `Apply-*.bat` file must live inside that top-level update folder and run from there.
- The apply script must automatically locate the project root.
- Every update must update `README.md`, `CHANGELOG.md`, `VERSION.md`, `ProjectManifest.json`, and this `AI_CONTEXT.md`.
- Never claim an update package exists unless it has actually been created and verified.

## Next Recommended Work

1. Finish Core configuration defaults and application-specific configuration overlays.
2. Finish logging levels, formatting, and startup log routing.
3. Add stronger repository health checks for duplicate models and application Core references.
4. Create or wire application project stubs so Server, Importer, Editor, Launcher, and Client explicitly reference Core.
5. Expand validation to include common game-data rules before Milestone 2 import work.
6. Continue updating this file after every Code Update.
