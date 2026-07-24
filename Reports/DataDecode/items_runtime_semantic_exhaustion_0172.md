# Items Runtime Semantic Exhaustion — CodeUpdate0172

## Executable advance

- Added evidence-gated owner quantity limits through `maximum_owned_quantity` instance metadata.
- Enforced limits during creation, import/add, pickup, give, merchant purchase, NPC transfer, and every ownership-changing transfer.
- Counted inventory, equipment, portable-container contents, bank storage, and trade staging as one owner scope.
- Released reservations when items become unowned, merchant-owned, NPC-owned, destroyed, or dropped.
- Added aggregate save-state validation and malformed-limit rejection.
- Added deterministic rollback and reservation-release smoke coverage.

## Evidence boundary

No Classic item is automatically marked limited. Native item-limit bits, account/world scopes, corpse/logout reservation rules, and exact messages remain explicit final-native-pass dependencies.
