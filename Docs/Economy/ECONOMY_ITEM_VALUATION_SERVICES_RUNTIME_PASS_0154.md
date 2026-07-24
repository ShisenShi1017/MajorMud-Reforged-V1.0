# Economy CodeUpdate0154 — Item Valuation and Economic Services

## Implemented runtime

This pass adds deterministic, checked item valuation; policy-driven buy, sell, appraisal, charge, condition, and repair projections; unified provider-owned service transactions; external effect and persistence boundaries; complete snapshot rollback; replay protection; and economic-event payments integrated with the existing atomic wallet transfer engine.

## Transaction order

The engine resolves the canonical service, validates provider ownership and availability, validates character and referenced-state eligibility, calculates cost, checks funds, deducts atomically, invokes the owning subsystem effect, coordinates persistence, commits replay state, and returns a deterministic result. Any exception restores wallet, character, provider, and world snapshots.

## Ownership boundary

Economy owns cost calculation, payment validation, deduction, refund-by-rollback, transaction state, replay state, and persistence coordination. Character, spell, ability, combat/death, movement, guild, inn/recovery, quest, script, textblock, command, and world-event systems own their gameplay effects through `IRuntimeEconomicServiceEffect`. No unsupported gameplay effect is invented.

## Native dependencies

Exact native buy, sell, appraisal, repair, trainer, resurrection, inn, teleport, guild, quest, script, textblock, command, and event constants and modifier order remain UNKNOWN pending WCC/WBB/native DLL exhaustion. They are explicit policy inputs. Unsafe missing values fail closed.

## Save and world impact

Wallet and item state, learned/rank state, provider state, replay keys, and callback-owned world state participate in atomic rollback. Versioned service persistence schema 1 covers replay and item-instance state.
