# DataSimVer0014 — Inventory, Equipment, and Economy State Simulation

## Objective
Execute a real measured pass against the DataSimVer0013 cumulative baseline to model item purchases, equipment attempts, sell-back transactions, and persistent inventory/gold/weight state.

## What was simulated
- 6 persistent virtual player profiles.
- Buy attempts across resolved shop inventory references.
- Equip attempts only after successful purchases.
- Sell-back attempts only after successful purchases.
- Persistent per-profile gold, item count, slot occupancy, and carried weight.

## Measured source inventory
- Items analyzed: 1950
- Shops analyzed: 178
- Shop inventory references: 3550
- Resolved shop inventory references: 1204
- Missing shop inventory references: 2346
- Positive-cost items: 1309
- Positive-weight items: 1791

## Simulation statistics
- Action attempts logged: 10456
- Expected-model successes: 3357
- Expected-model failures: 7099

## Success categories
- buy: 1616
- sell: 1506
- equip: 235

## Failure categories
- insufficient_gold: 3271
- shop_stock_empty: 1962
- not_equippable_by_data: 828
- slot_occupied: 453
- zero_cost_or_noncommercial_item: 227
- weight_limit_exceeded_inferred: 124
- zero_sell_value_inferred: 110
- stat_requirement_not_met: 100
- above_shop_max_level: 24

## Discoveries
- Shop inventory, item cost, item weight, wear slots, armor flags, weapon types, uses, and restrictions are present as data fields and can drive a generic transaction/equipment model.
- The model can maintain persistent virtual player state without touching `/Data`.
- Many failures are prerequisite failures: not enough gold, inferred carry limit, non-equippable items, or occupied equipment slots.
- Sell value and maximum carry weight are still inferred expected-model mechanics and are not promoted as original engine facts.

## Changes
- Added `/Simulator/DataSimVer/0014/InventoryEconomyActionLog.csv`.
- Added `/Simulator/DataSimVer/0014/PersistentInventoryState.json`.
- Added `/Simulator/DataSimVer/0014/FailureReasonCatalog.json`.
- Added `/Evidence/DataSimVer0014/ItemEconomyEvidenceExtract.csv`.
- Added `/Evidence/DataSimVer0014/ShopInventoryEconomyEvidence.csv`.
- Added validation summaries for transaction consistency and `/Data` checksum preservation.

## Required status checks
- `/Data`: unchanged.
- Room endpoints promoted: 0.
- Special-exit semantics promoted: 0.

## Remaining unknowns
- Exact original sell-back percentage.
- Exact carry weight / encumbrance algorithm.
- Exact equipment slot meanings for every numeric `worn_on` value.
- Class/race restriction numeric mapping where item restrictions refer to legacy indexes.
- Shop restock timing and runtime inventory mutation behavior.
- Whether bank/storage mechanics participate in shop transactions.

## Next pass
DataSimVer0015 should focus on combat, spell-state, and status-effect simulation, using the item/equipment model from this pass as input.

## Session health
Healthy. This was a real measured pass from the uploaded 0013 cumulative ZIP. Evidence boundaries are preserved.
