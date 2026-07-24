# Items Runtime Semantic Exhaustion — CodeUpdate0170

## Pass

Partial-quantity bank and merchant stack lifecycle.

## Executable advancement

- Bank deposits and withdrawals now accept explicit quantities.
- Merchant sales and purchases now accept explicit quantities.
- Partial operations split the source stack inside the existing atomic rollback boundary.
- The source instance identity remains attached to the remainder.
- The transferred quantity receives one new runtime instance identity.
- Binding, quest ownership, no-bank, and no-sell checks run before mutation.
- Invalid quantities, non-stackable partial operations, and failed validation roll back completely.
- Full-quantity operations preserve the original instance identity.
- Pricing and currency mutation remain outside Items.

## Evidence boundary

The generated Classic projection currently exposes no stackable items. The lifecycle primitive is executable Reforged compatibility infrastructure, but Classic partial transactions remain inactive until stackability and maximum-stack fields are proven or strongly correlated.

## Native dependencies

- Native stackability flags and maximum quantities.
- Destination-stack auto-merge behavior.
- Merchant quantity rendering and finite-stock representation.
- Bank quantity limits and capacity.
- Economy transaction ordering and rollback coordination.
