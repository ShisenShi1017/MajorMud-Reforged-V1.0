# AI Context for MajorMud Reforged

Read this file first in every new ChatGPT window or development session. This is the living handoff document and must be updated with every Code Update.

MajorMud Reforged is a standalone game engine and toolset inspired by the original MajorMUD. Original MajorMUD files are import/reference material only. After import, `Data/` is the live editable game database and `ImportedDatabases/` is archive/reference only.

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
- Current Code Update: 0012
- Current Database Version: 0009
- Last Update Name: CodeUpdate-0012-Data-Validation-And-Service-Completion
- Milestone Estimate: 78-84% complete
- Next Work Mode: Continue Milestone 1 until every completion criterion is satisfied.

## Completed or Substantially Underway

| Deliverable | Status | Notes |
|---|---:|---|
| Core configuration system | Substantially underway | Runtime provider now supports defaults, JSON loading, environment overlays, and typed access. |
| Core logging system | Substantially underway | Source-level logger contract, in-memory logger, composite logger, and shared log entries exist. |
| Validation framework | Substantially underway | Shared validation results, severities, data-record validator, and JSON health checks exist. |
| Serialization framework | Substantially underway | JSON record loader/saver and serializer foundations exist. |
| Repository interfaces | Substantially underway | Read-only/editable repository contracts and in-memory repository exist in Core. |
| Shared data models | Substantially underway | Shared DataRecord/IReforgedDataRecord/GameEntity foundations prevent app-specific model drift. |
| Event system | Substantially underway | Event contracts, base events, publisher/subscriber contracts, and in-memory event bus exist. |
| Dependency injection/service registry | Substantially underway | Lightweight dependency-free service registry exists for application bootstrap. |
| Version management | Substantially underway | Version info and version manager exist. |
| Project manifest reader | Substantially underway | Shared manifest model/reader and manifest policy fields exist. |
| Repository health checker | Substantially underway | Health models plus update package, app Core-reference, and Data JSON health checks exist. |
| Shared utility library | In progress | Path safety exists; more utilities should be added only when needed. |
| Unit-test framework | Started | Dependency-free assertions and test result model exist. |
| Applications using Core | Substantially underway | Server, Importer, Editor, Launcher, and Client source shells reference Core; Bin runtime artifacts remain future work. |

## Milestone 1 Completion Checklist

| Requirement | Status |
|---|---:|
| No duplicated models across applications | In progress |
| Every application references Core | Substantially underway |
| Core has no dependencies on application projects | Verified by structure |
| Core configuration fully functional | Substantially underway |
| Core logging complete | Substantially underway |
| Validation framework complete | Substantially underway |
| Serialization framework complete | Substantially underway |
| Repository layer complete | Substantially underway |
| Event system complete | Substantially underway |
| Dependency injection/service registry complete | Substantially underway |
| Version management complete | Substantially underway |
| Project manifest reader complete | Substantially underway |
| Repository health checker complete | Substantially underway |
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
- Every Code Update and Database Version ZIP must contain exactly one top-level folder named for the update, such as `CodeUpdate-0012-Data-Validation-And-Service-Completion/`.
- Users must be able to extract the ZIP directly into the main project folder without manually creating an update folder.
- The `Apply-*.bat` file must live inside that top-level update folder and run from there.
- The apply script must automatically locate the project root.
- Every update must update `README.md`, `CHANGELOG.md`, `VERSION.md`, `ProjectManifest.json`, and this `AI_CONTEXT.md`.
- Never claim an update package exists unless it has actually been created and verified.

## Code Update 0012 Handoff Notes

Core Update 0012 adds source-level runtime services and validation needed to close Milestone 1. It does not change the no-SDK runtime rule; these are source foundations for future compiled/runtime-ready artifacts.

Added Core areas:

- `RuntimeConfigurationProvider` for defaults, JSON config files, environment overlays, and typed access.
- Source-level `IReforgedLogger`, `InMemoryReforgedLogger`, and `CompositeReforgedLogger`.
- Source-level `ServiceRegistry` for dependency-free app bootstrap.
- Source-level `IDataRepository<TRecord>`, `IReadOnlyDataRepository<TRecord>`, and `InMemoryDataRepository<TRecord>`.
- `JsonDataFile` for shared JSON data record loading/saving.
- `DataRecordValidator<TRecord>` for record identity validation.
- `DataJsonHealthCheck` for parse/root/duplicate-ID checks against `Data/*.json`.
- `ApplicationCoreReferenceHealthCheck` for SDK-free app-to-Core reference verification.

## Next Recommended Work

1. Finish duplicate model removal/enforcement checks.
2. Expand Data JSON validation into cross-reference checks across rooms, items, monsters, shops, spells, quests, and world data.
3. Add repository-backed loaders for every current `Data/` category.
4. Prepare compiled/runtime-ready `Bin/` artifact expectations without requiring SDK use by normal users.
5. Produce a final Milestone 1 verification report once all checklist items are satisfied.
