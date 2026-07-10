# Patch Instructions — CodeUpdate0135 Final

Apply this update-only patch over the verified CodeUpdate0134 repository. It supersedes all earlier CodeUpdate0135 patches and addenda. In Codex, this gate passed locally and GitHub Actions remains pending until the user pushes.

From repository root run:

```powershell
python Tools/Validation/validate_codeupdate0135_exhaustive_repairs.py
python Tools/Validation/run_complete_runtime_verification.py

dotnet restore Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-room-exits
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-npcs
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-combat

dotnet restore Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj
dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore
dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build
```

Promotion requires zero failures.
