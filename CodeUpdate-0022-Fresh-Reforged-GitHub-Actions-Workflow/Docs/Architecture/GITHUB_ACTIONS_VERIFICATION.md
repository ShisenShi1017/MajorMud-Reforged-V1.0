# GitHub Actions Build Verification

Reforged uses GitHub Actions as the recommended build verification path so the user does not need to install the .NET SDK locally.

## Repository visibility

The repository may remain private. GitHub Actions can run in private repositories for authorized users.

## Workflow

The workflow file is:

- `.github/workflows/dotnet-build.yml`

It restores and builds:

- `Core/Source/Reforged.Core/Reforged.Core.csproj`
- `Apps/Source/Reforged.Server/Reforged.Server.csproj`
- `Apps/Source/Reforged.Importer/Reforged.Importer.csproj`
- `Apps/Source/Reforged.Editor/Reforged.Editor.csproj`
- `Apps/Source/Reforged.Launcher/Reforged.Launcher.csproj`
- `Apps/Source/Reforged.Client/Reforged.Client.csproj`

## How to verify an update

1. Apply the Code Update locally or replace the repository contents with the updated package.
2. Push the change to the private GitHub repository.
3. Open **Actions** in GitHub.
4. Select **Reforged Cloud Build**.
5. Confirm the latest run is green.
6. If it fails, copy the failed log output and use it as the next fix target.

## Rule

Do not claim an update is build-verified unless the GitHub Actions run or another real SDK-equipped environment reports success.
