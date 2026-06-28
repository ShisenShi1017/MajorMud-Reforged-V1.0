# Changelog

## CodeUpdate-0013 - Duplicate Model Enforcement

- Continued Milestone 1 Core Framework work.
- Added dependency-free duplicate model enforcement for application source.
- Added duplicate model enforcement issue/report models.
- Integrated duplicate model enforcement into the repository health checker.
- Added source-level duplicate model enforcement health check under `Core/Source/Reforged.Core/Health/`.
- Updated `AI_CONTEXT.md` with current handoff table, checklist, and next recommended work.
- Updated `README.md`, `VERSION.md`, `ProjectManifest.json`, docs, and verification report.
- Preserved no-SDK runtime policy, one-folder update ZIP policy, and protection of `Saves/` and `ImportedDatabases/`.


## CodeUpdate-0012 - Data Validation and Service Completion

- Continued Milestone 1 Core Framework work.
- Added source-level runtime configuration provider with defaults, JSON loading, environment overlays, and typed access.
- Added source-level Core logger contract, in-memory logger, and composite logger.
- Added dependency-free service registry for application bootstrap wiring.
- Added source-level data repository contracts and in-memory repository implementation.
- Added shared JSON data file loader/saver for Core data records.
- Added data-record validator for StableId, LegacyId, duplicate, and name checks.
- Added Data JSON health check for parse errors, invalid roots, and duplicate IDs.
- Added SDK-free application Core-reference health check.
- Updated `AI_CONTEXT.md` with current Milestone 1 status, handoff table, and completion checklist.
- Updated `README.md`, `VERSION.md`, `ProjectManifest.json`, and docs.
- Preserved no-SDK runtime policy, one-folder update ZIP policy, and protection of `Saves/` and `ImportedDatabases/`.

## CodeUpdate-0010 - Core Services and Handoff

- Continued Milestone 1 Core Framework work.
- Added `IConfigurationProvider`, configuration entries, JSON config loading, environment loading, and typed integer access.
- Added logging sink contract, composite logger, and in-memory log sink.
- Added validation merge helpers and reusable required-string validator.
- Added shared JSON file store.
- Added in-memory data repository for dependency-free staging and tests.
- Added update package health check for the required one-folder update ZIP layout.
- Updated application README files with the Core dependency rule.
- Updated `AI_CONTEXT.md` into a living handoff document with the completed/substantially-underway table and Milestone 1 completion checklist.
- Updated architecture/update documentation with the new update-folder packaging rule.
- Updated `README.md`, `VERSION.md`, and `ProjectManifest.json`.
- Preserved `Saves/` and `ImportedDatabases/`.

## CodeUpdate-0007 - Core Contracts Foundation

- Rebuilt Core Update 0007 from scratch.
- Added Core event contracts, publisher/subscriber interfaces, and data change event model.
- Added service registry contracts.
- Added validation pipeline interface and dependency-light implementation shell.
- Added repository interfaces for editable data and world data access.
- Added provenance tracking contracts.
- Added runtime state model and server-ready world model interface.
- Added runtime execution policy: normal project runtime must not require the .NET SDK.
- Added update execution policy: update package must run from its own extracted folder and apply into the main project folder.
- Preserves `Saves/` and `ImportedDatabases/`; neither folder is included in this package.
## CodeUpdate-0008 — Governance and Core Services

- Continued Milestone 1 Core Framework work.
- Added `AI_CONTEXT.md` as a fast entry point for new ChatGPT windows and developers.
- Added architecture governance documents under `Docs/Architecture/`.
- Added permanent rule: approved suggestions become required implementation work unless explicitly deferred or rejected.
- Added Core configuration provider, serializer contract/implementation, event bus, service registry, manifest reader, health checker, path safety helper, and version manager.
- Updated project manifest with development status tracking.
- Preserved no-SDK runtime policy and extracted-folder update execution rule.
## CodeUpdate-0009 — Core Audit and Test Foundation

- Continued Milestone 1 Core Framework work.
- Added `IReforgedDataRecord` so applications can share one Core data-record identity contract.
- Made `GameEntity` extensible instead of sealed to prevent future duplicate model definitions.
- Added read-only repository and repository catalog contracts.
- Added Core reference audit and duplicate model audit models for Milestone 1 completion tracking.
- Added dependency-free Core testing helpers and `Tests/Core/README.md` without adding any normal-runtime SDK requirement.
- Updated README, VERSION, ProjectManifest, documentation, release notes, and verification report.
- Preserved no-SDK runtime policy, extracted-folder update execution, and protection of `Saves/` and `ImportedDatabases/`.



## CodeUpdate-0011 - App Core Wiring and Runtime Audit

- Added Core application identity and common application contracts.
- Added source-level application shells for Server, Importer, Editor, Launcher, and Client.
- Wired every application source shell to reference Core.
- Added SDK-free application Core-reference audit support.
- Added runtime `Bin/` handoff requirement and validator models.
- Updated `AI_CONTEXT.md` with the living Milestone 1 handoff table and completion checklist.
- Reinforced the one-top-level-folder update ZIP rule in project documentation.
- Preserved the no-.NET-SDK runtime requirement.
