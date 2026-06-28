# CodeUpdate-0022 - Fresh Reforged GitHub Actions Workflow

## Purpose

Create a clean GitHub Actions workflow for the current rebranded repository layout after the uploaded baseline showed an empty `.github/workflows` folder.

## Changes

- Added `.github/workflows/dotnet-build.yml`.
- Added visible copy at `GitHubActions/dotnet-build.yml`.
- Workflow restores/builds:
  - `Core/Source/Reforged.Core/Reforged.Core.csproj`
  - `Apps/Source/Reforged.Server/Reforged.Server.csproj`
  - `Apps/Source/Reforged.Importer/Reforged.Importer.csproj`
  - `Apps/Source/Reforged.Editor/Reforged.Editor.csproj`
  - `Apps/Source/Reforged.Launcher/Reforged.Launcher.csproj`
  - `Apps/Source/Reforged.Client/Reforged.Client.csproj`
- Removed the previous stale-path guard to avoid self-matching false failures.

## Verification

- Confirmed the workflow contains no `Reforged.*` project paths.
- Confirmed `.github/workflows/dotnet-build.yml` exists in the package.
- Local compile was not run because this environment does not provide the .NET SDK. GitHub Actions remains the build verification source.

## Protected Data

`Saves/` and `ImportedDatabases/` are excluded from this update package.
