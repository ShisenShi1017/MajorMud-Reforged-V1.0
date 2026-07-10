# CodeUpdate0134 Patch Instructions

Apply this update-only archive at the repository root, preserving paths.

This archive supersedes every earlier CodeUpdate0134 archive.

Run:

```powershell
python Tools/Validation/validate_codeupdate0132_runtime_room_exit_slice.py
python Tools/Validation/validate_codeupdate0134_runtime_npc_loading.py

dotnet restore Apps/Compiler/MajorMud.Reforged.Compiler.csproj
dotnet build Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-restore
dotnet run --project Apps/Compiler.Tests --configuration Release

dotnet restore Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-room-exits
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --smoke-npcs

dotnet restore Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj
dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore
dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build
```

Expected NPC validation:

- 962 spawns
- 10 represented maps
- 748 linked population records
- 0 missing map identities
- 0 linked records missing map context
- 0 unresolved monster groups
- 0 unresolved boss definitions

Then run the local console separately:

```powershell
dotnet run --project Apps/Source/Reforged.Server --configuration Release -- --run-local
```
