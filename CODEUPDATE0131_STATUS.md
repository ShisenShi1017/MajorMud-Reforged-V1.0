# CodeUpdate0131 Status

Status: **server smoke-test prep ready**

- No-runtime generated Classic audit: PASS
- Missing core relationship refs: 0
- Runtime gameplay code changed: No
- Next proof must be performed with .NET SDK/Codex.

Run:

```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```
