# CodeUpdate-0016 Verification

## Local Environment

- Package structure checked.
- Changed file list checked.
- Protected folders checked.
- .NET SDK was not available in this environment, so local compile verification could not be run here.

## Cloud Build Verification Added

This update adds `.github/workflows/dotnet-build.yml`.

When the repository is pushed to GitHub, GitHub Actions will install .NET 8 in the cloud and run:

```text
dotnet restore Core/Source/Reforged.Core/Reforged.Core.csproj
dotnet build Core/Source/Reforged.Core/Reforged.Core.csproj --configuration Release --no-restore
```

This gives build verification without requiring the user to install the .NET SDK locally.

## Protected Folder Check

This package does not include or overwrite:

- `Saves/`
- `ImportedDatabases/`
