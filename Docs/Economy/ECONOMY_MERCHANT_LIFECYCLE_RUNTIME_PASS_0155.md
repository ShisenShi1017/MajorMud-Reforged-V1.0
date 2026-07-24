# Economy CodeUpdate0155 — Merchant Lifecycle and Restocking

## Runtime implementation

This pass adds a merchant lifecycle runtime on top of the CodeUpdate0151 merchant/shop stock owner. It implements deterministic availability, shop-hour evaluation, visibility, categories and customer restrictions, elapsed restock-cycle processing, fixed-quantity replenishment, fill-to-target replenishment, deterministic weighted generated inventories, maximum-stock enforcement, persistence coordination, rollback and replay protection.

## Transaction order

1. Resolve the canonical lifecycle definition.
2. Validate merchant and shop ownership against `RuntimeMerchantStock`.
3. Evaluate explicit lifecycle availability.
4. Evaluate shop schedule and customer restrictions.
5. Determine elapsed restock cycles.
6. Capture merchant stock and lifecycle snapshots.
7. Select generated entries deterministically when configured.
8. Apply capped checked stock additions.
9. Advance restock timestamps and versions.
10. Persist the resulting lifecycle snapshot.
11. Commit the replay key.
12. Return a deterministic result.

Any exception restores stock quantities, stock version, lifecycle timestamps, generated-item membership, versions and replay state.

## Native boundary

No MajorMUD-native shop-hour constants, restock intervals, generated inventory counts, weights, random seed, category IDs or ordering were invented. These values are explicit policy inputs. Missing unsafe definitions fail validation. The stable weighted selector is a deterministic fallback and remains marked as non-native until the final WCC/WBB/DLL binding pass.

## Ownership and persistence

Economy owns merchant cost and stock lifecycle state. World time supplies the evaluation instant. Character state supplies access context only. Persistent state includes stock, explicit availability, last and next restock time, restock and lifecycle versions, generated item membership and committed replay keys.
