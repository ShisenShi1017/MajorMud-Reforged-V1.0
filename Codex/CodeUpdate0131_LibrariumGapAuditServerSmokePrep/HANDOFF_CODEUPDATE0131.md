# Codex Handoff — CodeUpdate0131

Goal: prove the server can boot from Librarium-generated Classic data.

## Start here
Run from repo root:

```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```

## Expected result
- Build should succeed.
- `--smoke-generated-data` should load `Data/Classic` and report generated families with no diagnostics.
- `--run-local` should enter the Classic local console and show the starting room if runtime loading is working.

## No-runtime audit already passed
Audit file:
- `Reports/CodeUpdate0131/librarium_gap_audit_0131.json`
- `Reports/CodeUpdate0131/LIBRARIUM_GAP_AUDIT_0131.md`

## Important rules
- Librarium remains source of truth.
- `/Data` is generated output.
- Runtime must not edit Librarium.
- ImportedDatabases remain until Librarium is truly complete.
- Classic is locked/evidence-backed.
