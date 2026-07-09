# MajorMUD Reforged - CodeUpdate 0127A

## Current Baseline

CodeUpdate 0127A is the current corrective local baseline. It preserves the CodeUpdate 0127 world overlay/compiler completion work and repairs the clean-checkout Server bootstrap failure by tracking `Bin/.gitkeep` while keeping generated `Bin/` contents ignored.

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
Librarium → validate/export → /Data/<world> → server runtime → client read-only view
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

Continue Librarium population and Step 4: Librarium → `/Data` generator/compiler. Runtime gameplay code remains unchanged in this correction update.

## CodeUpdate 0126 — Librarium Compiler v1

The repository now includes the first Librarium compiler source in `Apps/Compiler` and generated Classic runtime output in `Data/Classic`. The Librarium remains the source of truth; `/Data` is generated output and must not be hand-edited.

Run with .NET when available:

```bash
dotnet run --project Apps/Compiler -- build --world Classic --clean --root .
```
