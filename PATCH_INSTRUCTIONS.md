# CodeUpdate0134 Patch Instructions

1. Start from committed CodeUpdate0133 baseline `96834067` with a clean tree.
2. Extract this archive into the repository root, preserving paths.
3. Confirm protected folders are not staged: `Saves/`, `ImportedDatabases/`, and `Reports/TempReview/`.
4. Run:

```powershell
python Tools\Validation\validate_codeupdate0134_runtime_npc_loading.py
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --smoke-room-exits
dotnet run --project Apps/Source/Reforged.Server -- --smoke-npcs
dotnet run --project Apps/Source/Reforged.Server -- --run-local
dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore
dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build
```

During `--run-local`, enter a generated diagnostic room that contains a spawn and verify that `look` lists the generated NPC name. Do not treat an unresolved spawn as permission to invent an NPC mapping.

The authoring environment did not have the .NET SDK. Promotion requires all local commands to pass.
