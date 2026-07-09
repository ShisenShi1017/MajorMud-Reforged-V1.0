# CodeUpdate-0067 Report — Librarium 0012–0014 Combined

Status: COMPLETE WITH TRACKED GAPS

## Scope
Completed a combined Librarium work package for:

- Librarium-0012 Local Graph Builder
- Librarium-0013 Query Engine
- Librarium-0014 Knowledge Gap Registry

The Librarium remains canonical. Generated graph/index artifacts are marked generated and are not promoted as source truth. MMUD Explorer and MBBSEmu were registered as external evidence repositories only.

## Generated local artifacts

- `Generated/LibrariumLocal/core_graph.json`
- `Generated/LibrariumLocal/formula_index.json`
- `Generated/LibrariumLocal/runtime_index.json`
- `Generated/LibrariumLocal/query_index.json`

Counts:

- Canonical entities consumed: 20826
- Canonical assertions consumed: 71026
- Formula records consumed: 10
- Generated graph nodes: 20838
- Generated graph edges: 71056

## Tools added

- `Librarium/Tools/build_local_graph_0012.py`
- `Librarium/Tools/query_engine_0013.py`
- `Librarium/Tools/generate_gap_registry_0014.py`

## External evidence repositories

- MMUD Explorer: 49 indexable source files
- MBBSEmu: 494 indexable source files

These sources are evidence only. They do not override Librarium records or introduce independent game truth.

## Knowledge Gap Registry

Created `Librarium/GapRegistry/knowledge_gap_registry_0014.json` with 46 tracked gaps.

High-priority formula/runtime gaps remain:

- Combat equations
- XP equations
- Regen equations
- Loot equations
- Spawn timing
- AI weighting
- Damage rounding
- Tick timing

## Runtime/Data impact

No gameplay formulas were replaced. `/Data` remains generated output and was not manually promoted from external repositories.

## Next recommended task

Begin Librarium-0015 Compiler Export using the local graph and gap registry as inputs.
