# MajorMUD Reforged - CodeUpdate 0134

## Current Baseline

CodeUpdate 0134 is the current local baseline. It builds on the 0132 room/exit runtime slice and 0133 developer-avatar session infrastructure by loading complete map-qualified Classic NPC population records into the server runtime. Spawns attach by `(map_id, room_number)` rather than global room number so future-map population does not contaminate Map 1. Librarium remains authoritative; generated `/Data/Classic` is runtime output; Omniverse evidence is validation material only until explicitly promoted through review.

Official rule set:

- **Librarium is the authoritative source of truth.**
- **Host/server/editor modifies the Librarium.**
- **Client is read-only and receives the active world's view.**
- **/Data is generated runtime output per world.**
- **/ImportedDatabases is retained evidence until the Librarium is 100% complete.**
- **Classic world definitions are locked/evidence-backed.**
- **Custom and AI worlds use explicit Librarium overlays.**

## Active Flow

```text
Librarium â†’ validate/export â†’ /Data/<world> â†’ server runtime â†’ client read-only view
```

## Corrected Metrics

- Canonical sources retained: **4**
- Canonical source files classified: **941**
- Text files scanned: **657**
- Binary/asset files classified: **284**
- Librarium object types defined: **24**
- Relationship edges defined: **16**

## Generate Runtime Data

Generate Classic runtime data locally with:

```bash
python Tools/Librarium/export_librarium_to_data.py --world classic --clean
```

## Important Documents

- `SOURCE_OF_TRUTH.md`
- `REPOSITORY_RULES.md`
- `Reports/CodeUpdate0120/repository_metrics_0120.md`
- `Codex/CODEUPDATE_0120_CODEX_HANDOFF.md`

## Next Work

Push the corrected CodeUpdate0134 map-qualified NPC population baseline and wait for green GitHub Actions before starting the next server slice. The next likely slice is combat execution readiness, but only after corrected 0134 is accepted. Do not add NPC AI, combat, loot, respawn timers, shops, economy, quests, persistence, or evidence promotion inside 0134.

## CodeUpdate 0126 â€” Librarium Compiler v1

The repository now includes the first Librarium compiler source in `Apps/Compiler` and generated Classic runtime output in `Data/Classic`. The Librarium remains the source of truth; `/Data` is generated output and must not be hand-edited.

Run with .NET when available:

```bash
dotnet run --project Apps/Compiler -- build --world Classic --clean --root .
```
