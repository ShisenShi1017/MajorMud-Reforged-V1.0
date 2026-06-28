# CodeUpdate-0024 — Core Verification

## Purpose

Begin Milestone 1 completion by adding verification-focused infrastructure only. This update does not add gameplay features.

## Changes

- Added a dependency-free Core event system:
  - `IReforgedEvent`
  - `ReforgedEventBase`
  - `EventPublishedEvent`
  - `IEventPublisher`
  - `InMemoryEventBus`
- Added startup diagnostics:
  - dependency injection resolution
  - runtime configuration defaults
  - event bus publish/subscribe/dispose verification
  - repository/data health checks
  - duplicate model enforcement check bridge
- Added `Reforged.Core.Tests` console smoke-test project.
- Updated GitHub Actions workflow to restore, build, and run the smoke tests after building all source projects.
- Restored `.github/workflows/dotnet-build.yml` as the active workflow location while keeping `GitHubActions/dotnet-build.yml` as the visible copy.

## Verification Scope

The smoke tests cover:

1. Dependency injection validation through `ServiceRegistry`.
2. Configuration loading and JSON flattening through `RuntimeConfigurationProvider`.
3. Event system publishing and subscription disposal.
4. Core data repository health loading.
5. Aggregated startup diagnostics.

## Notes

The local packaging environment used for this update did not include the .NET SDK, so final compile/test confirmation must be performed by GitHub Actions after upload. The workflow has been updated so the cloud build is the authoritative validation gate.


## Packaging Correction

CodeUpdate-0024 and all future Code Updates must be delivered as patch-only packages unless the user explicitly requests a full repository baseline. Each update ZIP must contain exactly one top-level folder, include an `Apply-CodeUpdate-XXXX.bat` installer, and include an `UpdateManifest.json` describing the changed/new files.
