# DataSimVer0012 — Service/NPC Interaction Resolution

## Objective
Resolve expected service and NPC interaction behavior using only existing `/Data`-derived evidence and prior DataSimVer0008 inventories.

## What was simulated
Virtual profiles attempted `talk`, `buy`, `train`, and spell-learning/service interactions against extracted shop/service records. Spawn records were converted into an NPC interaction matrix so combat-facing and spellcaster-facing rooms can be separated from economic/service rooms.

## Discoveries
- Shop/service records analyzed: 178
- Shop inventory references analyzed: 3550
- Room text service clues analyzed: 436
- Trainer room references retained: 13
- Spawn/NPC room references analyzed: 962
- Monster behavior records referenced: 1100
- Interaction attempts logged: 2851
- Expected-model successes: 1381
- Expected-model failures: 1470

## Failure categories
- above_max_level: 674
- service_room_missing_or_unresolved: 390
- service_clue_without_shop_record: 235
- below_min_level: 156
- transaction_verb_required_not_dialogue: 15

## Changes
- Added `/Simulator/DataSimVer/0012/ServiceResolutionMatrix.csv`.
- Added `/Simulator/DataSimVer/0012/ServiceInteractionLog.csv`.
- Added `/Simulator/DataSimVer/0012/NpcSpawnInteractionMatrix.csv`.
- Added `/Evidence/DataSimVer0012/` evidence extracts.
- Added validation summary for this pass.

## Required status checks
- `/Data`: unchanged.
- Room endpoints promoted: 0.
- Special-exit semantics promoted: 0.

## Remaining unknowns
- Exact original engine service opcodes are not yet verified.
- Shop type numeric meanings remain expected-model labels until original runtime or MBBSEmu confirms them.
- Dialogue text and service verbs may be generic parser behavior rather than per-NPC script behavior.
- Pricing, transaction side effects, trainer class checks, bank/vault semantics, and item restock timing need runtime comparison.

## Next pass
DataSimVer0013 should focus on inventory, economy, and item transaction simulation: buy/sell/carry/equip constraints, shop restock rules, and item usability.

## Session health
Healthy. The cumulative baseline was preserved and expanded without modifying authoritative `/Data`.
