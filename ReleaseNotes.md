# CodeUpdate-0020 - GitHub Actions Rebrand Path Repair

This repair update fixes the failed GitHub build after the source rebrand by making the workflow path validation explicit.

## Fixed

- Replaces the old workflow references to `Core/Source/MajorMud.Core/MajorMudCore.csproj` and `Apps/Source/MajorMud.*` projects.
- Builds the rebranded project paths:
  - `Core/Source/Reforged.Core/Reforged.Core.csproj`
  - `Apps/Source/Reforged.Server/Reforged.Server.csproj`
  - `Apps/Source/Reforged.Importer/Reforged.Importer.csproj`
  - `Apps/Source/Reforged.Editor/Reforged.Editor.csproj`
  - `Apps/Source/Reforged.Launcher/Reforged.Launcher.csproj`
  - `Apps/Source/Reforged.Client/Reforged.Client.csproj`
- Adds a workflow guard that fails early if an old `MajorMud.*` project path remains in `.github/workflows`.
- Adds a visible manual-copy workflow at `GitHubActions/dotnet-build.yml` in case the hidden `.github` folder is missed during upload.

## Note

The GitHub Node.js 20 message is a GitHub-hosted runner/action warning, not the C# build failure. The failure shown by the old log came from the restore/build step using old project paths.
