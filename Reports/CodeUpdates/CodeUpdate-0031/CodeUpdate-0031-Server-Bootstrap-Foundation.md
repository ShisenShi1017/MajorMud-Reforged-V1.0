# CodeUpdate-0031 - Server Bootstrap Foundation

## Purpose

Continue Milestone 2 with the next small host/server slice after CodeUpdate-0030 was reported green by the user.

## Implemented

- Added `Program.cs` for the Server project.
- Added `ServerBootstrap`.
- Added `ServerBootstrapReport`.
- Updated `Reforged.Server.csproj` to produce an executable project.
- Added Server bootstrap smoke coverage to the existing smoke-test project.
- Added `DEVELOPMENT_PRINCIPLES.md`.
- Updated continuity documents.

## Server bootstrap checks

The bootstrap verifies:

- Repository root discovery.
- Required folders: `Apps`, `Core`, `Data`, `Docs`, `Reports`, and `Bin`.
- `ProjectManifest.json` readability.
- Project identity is `Reforged`.
- Manifest has Code Update and Database Version fields.
- Existing Server startup diagnostics run and pass.

## Boundaries

This update does not add gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation.

## Verification

Verified before package creation:

- `AGENTS.md` exists in GitHub truth.
- CodeUpdate-0030 is current in GitHub truth.
- Server startup diagnostics source exists from CodeUpdate-0029.
- Existing smoke test project already references `Reforged.Server`.
- The ChatGPT packaging environment does not have the .NET SDK, so `dotnet restore`, `dotnet build`, and `dotnet run` could not be executed here.

Package verification before delivery:

- ZIP exists.
- ZIP has exactly one top-level folder.
- ZIP contains `Apply-CodeUpdate-0031.bat`.
- ZIP contains `UpdateManifest.json`.
- ZIP payload is patch-only and does not include `Saves/` or `ImportedDatabases/`.

GitHub Actions remains the official validation gate after user apply/commit/push.
