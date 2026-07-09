# Codex Task - CodeUpdate 0116 Runtime Milestone 2 Core Backbone

Apply the patch-only update, then verify the server in a real .NET environment.

## Strict constraints

- Fix compile errors only if needed.
- Do not redesign the architecture.
- Do not implement full combat, magic, NPC AI, spawns, quests, scripts, guilds, dialogue, or economy.
- Do not write durable save files.
- Preserve `Saves/` and `ImportedDatabases/`.

## Commands

```bash
dotnet restore Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- --run-local
```

## Smoke test commands

Inside local console, run:

```text
state
events
entities
occupants
slots
npc
combat
cast
quit
```

Report build status, runtime status, changed files, and any unresolved issue.
