# CodeUpdate 0132 — Runtime Room/Exit Load Slice

CodeUpdate 0132 is a narrow server-readiness slice.

It does not add combat, magic, NPC AI, economy, or quest runtime. It only makes the generated Classic world usable for a first room/movement test.

## Fixed

The prior known limitation was:

> Runtime local start still shows Unknown Room with no exits.

0132 addresses this by adding runtime-ready room fields and embedded exits to generated Classic room data.

## Evidence Used

Room names and room-to-room links are sourced from retained imported evidence:

- `ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/roommaps/`
- `ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/room-detail-inventory-wcc.csv`

`ImportedDatabases` remains retained until the Librarium is declared complete.

## New Server Command

```bash
dotnet run --project Apps/Source/Reforged.Server -- --smoke-room-exits
```

Expected result: JSON report with `Passed: true`, a real start room name, and resolved movement targets.

## Next Test Commands

```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --smoke-room-exits
printf "look\nnorth\nlook\nquit\n" | dotnet run --project Apps/Source/Reforged.Server -- --run-local
```

## Expected Local Result

The local console should start in:

```text
Town Gates
Exits: E, N, W
```

Moving north should resolve to:

```text
Estwall Street
```

## Still Not In Scope

- Combat execution
- Spell casting
- NPC AI
- Spawns/timers
- Shops/economy
- Quest triggers
- Persistence beyond session state
