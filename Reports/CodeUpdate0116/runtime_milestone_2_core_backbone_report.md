# CodeUpdate 0116 Report - Runtime Milestone 2 Core Backbone

## Baseline

- User-reported verified baseline: CodeUpdate 0115.
- User-reported local commit: 2fd0e3ee.
- Server was reported to run locally with runtime state/events verified.

## Changes

0116 adds contract-level runtime foundations to `Apps/Source/Reforged.Server`:

- `RuntimePersistenceContracts.cs`
- `RuntimeEntityRegistry.cs`
- `RuntimeBackboneFoundations.cs`
- updates to runtime session, session manager, command dispatcher, state store, network host, and runtime host wiring.

## Verification intent

After applying this patch, run:

```bash
dotnet restore Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- --run-local
```

Then verify local commands:

- `state`
- `events`
- `entities`
- `occupants`
- `slots`
- `npc`
- `combat`
- `cast`

## Limitations

This environment does not have the .NET SDK installed, so build verification must be done in Codex, GitHub Actions, Codespaces, or the user's local repo.
