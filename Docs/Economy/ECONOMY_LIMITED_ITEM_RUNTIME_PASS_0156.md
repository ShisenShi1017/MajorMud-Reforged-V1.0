# Economy CodeUpdate0156 — Limited-Item Runtime

This pass adds global accounting for limited and unique items without inventing native limits or timing constants.

## Runtime ownership
Economy owns reservation, population accounting, owner uniqueness, release, destruction, recycling, respawn readiness, replay protection, persistence coordination, and merchant/loot eligibility validation. Inventory, merchant, loot, quest, script, and world runtimes own their gameplay mutations and must coordinate those mutations with the ledger transaction.

## Deterministic lifecycle
The ledger counts Reserved, Active, and RespawnPending allocations against a policy-supplied world limit. Creation reserves capacity before an item is exposed. Activation binds the allocation to its runtime owner. Release removes it from committed population. Destruction deterministically chooses Destroyed, Recycled, or RespawnPending from policy. Due respawns return to a reserved system pool.

## Atomicity
Every mutating operation snapshots the ledger, validates identity, replay key, source eligibility, uniqueness, and limits, writes the state, persists it, then commits. Any exception restores allocations, version, ownership, timing, and replay state.

## Integrations
Merchant generation and loot creation receive explicit eligibility gates and reservation contracts. Existing merchant stock, loot corpse, inventory, and wallet code is not duplicated.

## Native dependencies
Classic item limits, creation ordering, release semantics, recycle behavior, respawn delays, and DLL execution ordering remain UNKNOWN until the final WCC/WBB/native pass. Runtime values are explicit policy inputs. Missing unsafe policy fails closed.
