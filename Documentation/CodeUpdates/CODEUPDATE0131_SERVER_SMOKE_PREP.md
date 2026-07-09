# CodeUpdate0131 Server Smoke Prep

CodeUpdate0131 is not a gameplay milestone. It is a readiness checkpoint before runtime proof.

It verifies that the generated Classic payload is internally coherent enough for the .NET server smoke test:

```text
Librarium -> generated Data/Classic -> server smoke loader
```

## What was proven without runtime
- Required generated runtime families exist.
- Generated family JSON is readable.
- Core references checked by the audit resolve.
- NPC/spell formula references now resolve against `Data/Classic/formulas.json`.
- Room spawn references now match the actual generated spawn records.

## What still requires Codex/.NET
- Server project build.
- Generated-data smoke run.
- Local runtime console start.
- First Classic world entry test.

## Commands
```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```
