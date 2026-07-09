# Codex Handoff — CodeUpdate0130 Server Classic Test Readiness

## Goal

Get the server running tomorrow night far enough to run Classic smoke tests from generated `Data/Classic`.

## What changed

- Combat formula conflicts were closed using DLL-derived evidence.
- The generated Classic conflict catalog was updated.
- Server source now includes generated-world validation catalog code.
- Server CLI now has `--smoke-generated-data` / `--validate-generated-data` path.

## Required commands

From repo root:

```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```

## Expected behavior

`--smoke-generated-data` should:

- load `Config/server.worlds.json`,
- load world `classic`,
- validate generated `Data/Classic`,
- validate `world_manifest.json` and `compile_manifest.json`,
- load rooms and start room,
- print a JSON smoke report.

## Known limitation

This ChatGPT environment did not have the .NET SDK, so the C# path is source-prepared and Python-validated but not built here.

## If build fails

Fix compile errors in the smallest possible patch. Do not change architecture. Preserve:

- Librarium source of truth,
- `ImportedDatabases` retained,
- `/Data` generated output,
- Classic protected,
- client read-only.
