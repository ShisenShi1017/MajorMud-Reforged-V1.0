# Classic Economy and Service Closure — CodeUpdate0139

The generated Classic economy/service corpus is exhaustively classified and connected to live runtime consumers.

- 178/178 shops
- 26/26 executable room-bound banks/vaults (`bank.2` / `Leave this blank` remains Librarium-only)
- 44/44 trainers
- 1,204/1,204 shop inventory relationships
- 1,950/1,950 item economy records
- 23/23 economy rules
- 204 classified shop/bank services
- 0 unclassified services

Runtime repairs:

- Live buy pricing now applies each shop's decoded `markup_percent` through the native markup authority.
- Persistent restocking now consumes decoded `regen_time`, `regen_qty`, `regen_percent`, `normal_qty`, and `max_qty`.
- Regeneration percentage uses a deterministic, replay-safe 1..100 cycle roll.
- Entries with zero regeneration time, quantity, or percentage are explicitly non-restocking instead of being forced onto a six-second timer.
- Existing five-denomination banking, trainer restrictions/progression, persistent stock, buy/sell ownership transfer, currency settlement, repair fail-closed behavior, and transaction rollback remain authoritative.

Exact sell-price modifiers, repair prices, or service costs absent from generated evidence remain deterministic failure paths rather than invented constants. Empty Recycler inventories accept sold stock through the existing player-stocked merchant path and cannot sell nonexistent inventory.

Verification:

- `--audit-classic-economy-services`: PASS
- Compiler/server Release builds: PASS, 0 warnings/errors
- Bundle C economy/merchant audits: PASS
- World systems/service economy smokes: PASS
- Bundle E merchant/trainer and relationship/service audits: PASS
