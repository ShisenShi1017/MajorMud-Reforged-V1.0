# Changelog

## CodeUpdate-0025 - Rebrand Completion Audit

- Completed a rebrand audit for active source, project, assembly, namespace, workflow, and importer utility references.
- Removed obsolete duplicate Tools/Importer/MajorMudDatDecoder.py; Tools/Importer/ReforgedDatDecoder.py remains the active DAT inspection tool.
- Recorded that remaining MajorMud references are historical, repository-name, or legacy-context references rather than active source/build identifiers.
- No gameplay feature work was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.
- Local .NET 8.0.422 restore/build/smoke-test verification passed; GitHub Actions remains the official gate after user push.

## CodeUpdate-0024B - Core Startup Diagnostics Logger Fix

- Corrected the CoreStartupDiagnostics logger call to use the verified InMemoryReforgedLogger.Write(LogSeverity.Information, ...) API.
- Fixed the CodeUpdate-0024 build failure caused by calling Info on the concrete in-memory logger type.
- Recorded CodeUpdate-0024B as a direct corrective patch with no gameplay feature work.
- No BAT installer or update package was created per current user instruction.

## CodeUpdate-0024 - Core Verification

- Added dependency-free Core event bus contracts and in-memory implementation.
- Added Core startup diagnostics for DI, configuration, event publishing, and repository health.
- Added `Tests/Source/Reforged.Core.Tests` smoke-test project.
- Updated GitHub Actions to restore, build, and run smoke tests.
- No gameplay feature work was introduced.


## CodeUpdate-0019 - Reforged Source Rebrand

- Continued Milestone 1 Core Framework work after GitHub verified CodeUpdate-0018.
- Rebranded source namespaces from the old source namespace patterns to `Reforged.*`.
- Renamed source project folders and project files to `Reforged.Core`, `Reforged.Server`, `Reforged.Importer`, `Reforged.Editor`, `Reforged.Launcher`, and `Reforged.Client`.
- Updated assembly names and root namespaces to the new `Reforged.*` names.
- Updated GitHub Actions cloud build paths to restore/build the renamed projects.
- Preserved the CodeUpdate-0018 Core application contract compile fix.
- Removed remaining old project-name text references from repository text content included in this update.
- Preserved no-local-SDK runtime policy, one-folder update ZIP policy, and protection of `Saves/` and `ImportedDatabases/`.

## CodeUpdate-0017 - GitHub Actions Runtime Verification

- Continued Milestone 1 Core Framework work.
- Added `.github/workflows/dotnet-build.yml` for cloud restore/build verification of Core and application source projects.
- Added `Bin/RuntimeArtifacts.json` to document future runtime-ready executable handoff locations.
- Added `BinRuntimeHandoffCatalog` and expanded `BinRuntimeHandoffValidator` for SDK-free runtime artifact expectation checks.
- Added GitHub Actions verification documentation, including the recommendation that the repository may remain private.
- Updated `README.md`, `VERSION.md`, `ProjectManifest.json`, and `AI_CONTEXT.md`.
- Preserved no-local-SDK runtime policy, one-folder update ZIP policy, and protection of `Saves/` and `ImportedDatabases/`.

## CodeUpdate-0016 - Repository-Backed Data Loaders

- Continued Milestone 1 Core Framework work.
- Added Core repository-backed loaders for current canonical `Data/` categories.
- Added flexible `CanonicalDataRecord` projection that preserves raw JSON fields while exposing common identity fields.
- Added `CoreDataCatalog` and category descriptors for shared application bootstrap use.
- Added Core data repository health checking and integrated it with Data JSON health checks.
- Added GitHub Actions `.NET Build Verification` workflow so build verification can run in GitHub without requiring the user to install the .NET SDK locally.
- Updated `AI_CONTEXT.md`, `README.md`, `VERSION.md`, `ProjectManifest.json`, documentation, and verification report.
- Preserved no-SDK runtime policy, one-folder update ZIP policy, and protection of `Saves/` and `ImportedDatabases/`.

## CodeUpdate-0015 - Data Cross-Reference Validation

- Continued Milestone 1 Core Framework validation work.
- Added dependency-free source-level `DataCrossReferenceHealthCheck` for Data JSON relationship validation.
- Integrated cross-reference checks into `DataJsonHealthCheck` after parse/root/duplicate-ID checks pass.
- Added validation result merging support to source-level Core validation results.
- Added checks for world room exits, room item references, room monster references, race class references, and future shop/quest/loot-table references.
- Updated `AI_CONTEXT.md`, `README.md`, `VERSION.md`, `ProjectManifest.json`, docs, and verification report.
- Preserved no-SDK runtime policy, one-folder update ZIP policy, and protection of `Saves/` and `ImportedDatabases/`.

## CodeUpdate-0014 - AI Handoff Standardization

- Continued Milestone 1 Core Framework governance and handoff work.
- Standardized `AI_CONTEXT.md` as the authoritative handoff document for new ChatGPT windows and development sessions.
- Added required session startup order, current repository facts, Milestone 1 status table, completion checklist, update history, and next recommended work.
- Added `Docs/24-CoreUpdate0014-AI-Handoff-Standardization.md` documenting the handoff standard and required future maintenance.
- Updated `README.md`, `VERSION.md`, and `ProjectManifest.json`.
- Preserved no-SDK runtime policy, one-folder update ZIP policy, and protection of `Saves/` and `ImportedDatabases/`.

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
## CodeUpdate-0008 ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Governance and Core Services

- Continued Milestone 1 Core Framework work.
- Added `AI_CONTEXT.md` as a fast entry point for new ChatGPT windows and developers.
- Added architecture governance documents under `Docs/Architecture/`.
- Added permanent rule: approved suggestions become required implementation work unless explicitly deferred or rejected.
- Added Core configuration provider, serializer contract/implementation, event bus, service registry, manifest reader, health checker, path safety helper, and version manager.
- Updated project manifest with development status tracking.
- Preserved no-SDK runtime policy and extracted-folder update execution rule.
## CodeUpdate-0009 ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Core Audit and Test Foundation

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

## CodeUpdate-0020 - GitHub Actions Rebrand Path Repair

- Repaired the GitHub Actions workflow after the Reforged source rebrand.
- Replaced old `MajorMud.*` restore/build paths with `Reforged.*` project paths.
- Added a workflow guard that detects stale `MajorMud.*` project paths under `.github/workflows`.
- Added `GitHubActions/dotnet-build.yml` as a visible manual repair copy if the hidden `.github` folder is missed.
- Preserved the no-SDK normal runtime policy and protected `Saves/` / `ImportedDatabases/`.

## CodeUpdate-0023 - Fresh GitHub Actions Workflow with BAT Installer

- Added `.github/workflows/dotnet-build.yml` for the rebranded `Reforged.*` project layout.
- Added `GitHubActions/dotnet-build.yml` as a visible manual-copy source.
- Added `Install-GitHub-Actions-Workflow.bat` to install the workflow into `.github\workflows` from Windows.
- Kept build verification cloud-based through GitHub Actions; no local .NET SDK is required for normal runtime use.
- Preserved protected runtime/user data folders from update packaging.
