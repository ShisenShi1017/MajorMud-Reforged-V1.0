# CodeUpdate-0041 Verification Report - Classic Parity Audit Foundation

## Verification Performed In This Package

- Patch package structure created with exactly one top-level folder.
- `Apply-CodeUpdate-0041.bat` included.
- `Apply-CodeUpdate-0041.ps1` included.
- `UpdateManifest.json` included.
- Changed/new files placed under `Payload/` using repository-relative paths.
- Package inspected after ZIP creation.

## Expected Local Verification After Applying

Run from the repository root after applying the patch:

```powershell
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj -c Release
dotnet run --project Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj -c Release
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -c Release -- .
```

Expected result:

- Build passes.
- Smoke tests pass.
- Server bootstrap prints Classic parity inventory status.
- Gameplay remains disabled.

## Known Limitations

This update does not parse raw imported data formats yet. It creates a safe audit foundation so follow-up updates can add exact parsers and field-level comparisons without rewriting canonical data.
