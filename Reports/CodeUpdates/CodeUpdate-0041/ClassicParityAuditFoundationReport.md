# CodeUpdate-0041 Verification Report - Classic Parity Audit Foundation

## Verification Performed In This Package

- Patch package structure created with exactly one top-level folder.
- `Apply-CodeUpdate-0041.bat` included.
- `Apply-CodeUpdate-0041.ps1` included.
- `UpdateManifest.json` included.
- Changed/new files placed under `Payload/` using repository-relative paths.
- Package inspected after ZIP creation.

## Local Verification After Applying

Run from the repository root after applying the patch:

```powershell
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore
dotnet build Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-restore
dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj --configuration Release --no-build
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- .
```

Result:

- Server Release build passed with 0 warnings and 0 errors.
- Core, Server, Importer, Editor, Launcher, Client, and Core.Tests Release builds passed sequentially with 0 warnings and 0 errors after an initial parallel Core.Tests build hit a transient Core `obj` file lock.
- Smoke tests passed.
- Server bootstrap printed Classic parity inventory status.
- Gameplay remains disabled.

## Known Limitations

This update does not parse raw imported data formats yet. It creates a safe audit foundation so follow-up updates can add exact parsers and field-level comparisons without rewriting canonical data.

All WCC data remains reference-only unless the user explicitly approves a reviewed import/reconciliation update.
