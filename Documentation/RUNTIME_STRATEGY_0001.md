# Runtime Strategy 0001

The Reforged runtime is split into three layers.

1. **Data Runtime**
   Loads `/Data`, exposes table summaries, and detects missing or malformed canonical files.

2. **Simulation Runtime**
   Runs offline deterministic validators and formula checks. This is where formulas are proven or rejected before game-server use.

3. **Game Runtime**
   Later milestone. Adds sessions, commands, world state, scheduler, persistence, multiplayer, and editor mutation.

Formula modes:

- `classic_proven`: exact classic behavior has evidence.
- `classic_hypothesis`: candidate classic behavior, not promoted.
- `mmexplorer_derived`: derived from MMExplorer surfaces but not yet proven against runtime logs.
- `reforged_formula`: intentional new Reforged behavior.
- `experimental`: temporary sandbox behavior.

Rule: no formula may be promoted to `classic_proven` without evidence.
