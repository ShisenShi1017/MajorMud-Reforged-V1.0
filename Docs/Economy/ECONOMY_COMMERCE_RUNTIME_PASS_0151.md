# Economy Commerce Runtime Pass 0151

This pass adds the executable merchant transaction layer above the denomination-aware currency runtime.

Implemented runtime behavior:

- Canonical item definitions with base value, stack, capacity, sellability, quest, and limited-item flags.
- Merchant/shop identity and merchant-wallet ownership binding.
- Limited and unlimited stock state.
- Purchasable and merchant-acceptance controls.
- Data-driven buy and sell modifiers expressed in basis points.
- Explicit floor, ceiling, and nearest-away-from-zero price rounding.
- Checked unit and quantity totals.
- Buy validation for stock, funds, stack limits, and inventory capacity.
- Sell validation for ownership, sellability, quest restrictions, merchant acceptance, merchant stock limits, and merchant funds.
- Atomic merchandise and currency transfer.
- Complete restoration of character wallet, merchant wallet, character inventory, and merchant stock after any failure.
- Versioned inventory and stock snapshots for later persistence binding.

The runtime does not assign MajorMUD-specific item prices, buy/sell percentages, charisma adjustments, reputation adjustments, stock counts, or restock clocks. Those values remain data-driven and Unknown (Native Dependency) until native or independently corroborated evidence proves them.
