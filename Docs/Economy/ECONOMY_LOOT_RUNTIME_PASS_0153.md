# Economy Loot Runtime Pass 0153

## Implemented

This pass adds the authoritative loot lifecycle without inventing MajorMUD-native table data:

- deterministic guaranteed and weighted loot resolution;
- checked currency and quantity arithmetic;
- corpse-owned currency and item state;
- private loot-right enforcement;
- expiry and cleanup guards;
- atomic claim-all transfer into the character wallet and inventory;
- rollback of corpse, wallet and inventory on any failure;
- corpse-local claim replay protection;
- versioned corpse persistence and validation;
- Librarium bidirectional ownership and transfer relationships.

## Execution order

Claim-all validates canonical IDs and currency compatibility, locks corpse/wallet/inventory in canonical identifier order, validates rights and expiry, captures all three states, applies items, applies currency, clears the corpse, records the claim ID, and returns resulting versions. Any exception restores every captured state.

## Evidence boundary

Server authority is proven by the project architecture. Atomic economic mutation and deterministic policy execution are strongly correlated with the already implemented currency, commerce, and banking contracts. Native MajorMUD loot-table contents, weights, rare and quest predicates, party distribution rules, public-loot timing, and corpse cleanup intervals remain UNKNOWN and are not activated.

## Persistence

Schema version 1 stores corpse identity, source entity, currency, owners, public state, unclaimed grants, creation/expiry timestamps, version, applied claim IDs, and cleanup state. Duplicate corpse IDs and malformed snapshots fail closed.

## Validation

The validator checks JSON, Python, C# structure, runtime references, canonical loot/corpse/item/currency/owner/claim identifiers, deterministic resolution contracts, claim ordering, rollback, replay protection, persistence schema, manifest completeness, and ZIP integrity.
