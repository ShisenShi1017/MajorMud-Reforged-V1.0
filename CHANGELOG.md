# Changelog

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

