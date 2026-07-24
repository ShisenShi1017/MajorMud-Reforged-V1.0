# Items Runtime Semantic Exhaustion — CodeUpdate0186

## Executable advancement

CodeUpdate0186 adds evidence-gated NPC corpse-loot target reconciliation. A caller that has resolved an authoritative NPC loot relationship may reconcile a canonical item and loot key to an explicit target quantity on an existing NPC corpse.

## Implemented semantics

- Requires a live corpse identity, canonical item ID, loot key, and nonnegative target.
- Preserves matching runtime identities.
- Creates only the deficit and is idempotent at the same target.
- Applies projected stack limits and destination merging.
- Rejects target reductions rather than silently deleting world state.
- Keeps probability rolls, loot-table selection, scheduling, and messages outside Items until proven.

## Evidence status

Strongly correlated and evidence-gated. The lifecycle and identity behavior are active; native loot mappings and random selection are not inferred.

## Native dependencies remaining

- Guaranteed, random, rare, and quest loot source records.
- Drop chances and quantity-roll formulas.
- Limited-item suppression ordering.
- Gold generation and exact messages.
