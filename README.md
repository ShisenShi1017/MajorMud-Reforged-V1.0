# MajorMUD Reforged - CodeUpdate 0137 Internal Character Foundation

## Current Baseline

CodeUpdate 0135 is green by user report, and the internal CodeUpdate0136 Classic drop-roll repair is green by user report. The current local work is CodeUpdate0137 internal Character Foundation: Librarium SQL/MDB-backed records generate the playable Classic race/class catalog, and the server runtime now has account/character persistence and telnet character selection plumbing.

The current generated Character Foundation catalog contains 13 playable races, 15 playable classes, and 195 classified race/class pairs. Exact compatibility, starting stats, HP/mana, skills, spells, equipment, and alignment remain blocked until evidence is promoted. Real character creation also blocks when no verified generated Classic character-start room exists; `room.1` remains diagnostic-only.

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

Push the internal CodeUpdate0137 Character Foundation slice and wait for green GitHub Actions before continuing. CodeUpdate0137 remains the active internal milestone for complete Classic gameplay, but it must not be published as a partial public package. Next work should resolve the generated Classic character-start evidence and then continue into exact compatibility, starting stats, skills/spells/equipment, and inventory/equipment runtime.

## CodeUpdate 0126 â€” Librarium Compiler v1

The repository now includes the first Librarium compiler source in `Apps/Compiler` and generated Classic runtime output in `Data/Classic`. The Librarium remains the source of truth; `/Data` is generated output and must not be hand-edited.

Run with .NET when available:

```bash
dotnet run --project Apps/Compiler -- build --world Classic --clean --root .
```
