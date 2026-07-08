# Runtime-0010 — Loot, Economy, and NPC Services

This cumulative milestone extends Runtime-0009-Combat-REDO with executable foundation modules for loot, corpse generation, shops/economy, banking, repair pricing, and NPC service quote pipelines.

## Accuracy rule
No exact Classic formula is claimed in this milestone. All newly added behavior is tagged `not_classic_claimed` until MMExplorer/MBBSEmu/runtime evidence proves exact mechanics.

## Added runtime modules

- `Runtime/loot/loot_runtime.py`
- `Runtime/economy/economy_runtime.py`
- `Runtime/npc_services/npc_services_runtime.py`
- `Sim/validators/validate_loot_economy_npc.py`

## Implemented foundation behavior

- Monster loot event pipeline
- Gold event pipeline using explicit fields when present
- Corpse object creation
- Shop buy/sell quote pipeline
- Bank deposit/withdraw state transition
- Repair quote hook
- Healer quote hook
- Inn quote hook
- Trainer quote hook
- Guild service hook
- Travel/teleport service hook

## Still unknown

- Classic loot drop probabilities
- Classic treasure table nesting rules
- Exact shop/charm formulas
- Exact trainer costs and requirements
- Exact healer and inn behavior
- Exact corpse decay timing
