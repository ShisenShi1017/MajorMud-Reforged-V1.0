# CodeUpdate 0106 — Economy Reconstruction (Redo)

Status: **PASS_WITH_LIMITATIONS**

This redo performs a real Economy subsystem pass using the uploaded 0105 World/Movement cumulative baseline plus attached MBBSEmu and MMUD Explorer sources.

## Metrics

- Raw economy evidence hits: **935,655**
- Files with economy hits: **3,570**
- Prioritized evidence references: **1,750**
- Relationship edges: **11,262**
- Merchant/shop/service candidate records: **2,419**
- Economy probes defined: **6**
- Economy gaps recorded: **8**
- Mechanics promoted: **0**
- Runtime behaviors replaced: **0**

## Promotion decision

One narrow data-presence record was promoted: `economy.data_presence.merchant_shop_references`. This verifies traceable merchant/shop/economy data presence only. It does not verify pricing formulas, merchant acceptance, repair math, banking persistence, loot value generation, or gambling behavior.

## Remaining economy gaps

- Exact buy/sell price formulas and rounding
- Merchant item acceptance/rejection rules
- Repair costs and durability semantics
- Bank persistence and limits
- Loot/treasure value generation
- Service fee constants
- Gambling odds and payouts
- Currency caps and overflow behavior

## Next branch

Proceed to **CodeUpdate 0107 — Quest Reconstruction** after review.
