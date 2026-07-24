# Character Native Dependency Boundary — CodeUpdate0166

## Classification

**PROVEN boundary policy.** CodeUpdate0165 established that the six remaining Character lifecycle questions cannot be resolved from the cumulative repository. CodeUpdate0166 does not promote any native behavior. It makes that closure decision executable and fail-closed.

## Runtime behavior

The lifecycle coordinator exposes typed, queryable decisions for exact welcome/event ordering, reconnect grace/socket takeover, link-dead combat retention, party leadership succession, trade escrow disposition, and offline corpse-decay reconciliation. Every decision remains `UNKNOWN`, requires explicit native executable or world-tick evidence, and is non-executable. Attempting to require one as executable throws before Character or world state is mutated.

The safe Reforged fallback for each dependency is recorded separately from native semantics. This prevents accidental claims that Reforged cleanup policy reproduces native timing or dispatcher behavior.

## Save and world-state impact

Boundary queries are read-only and do not modify Character saves. The execution gate prevents speculative socket takeover, retained combat ownership, invented party leadership transfer, escrow redistribution, offline corpse decay, or native message ordering.

## Closure

Character remains semantically exhausted against available evidence. The six typed dependencies are carried forward unchanged to the final WCC/WBB/native DLL pass.
