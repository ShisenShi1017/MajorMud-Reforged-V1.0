# Character Inventory Gold Pass 0159

## Promoted behavior

The Character runtime owns carried currency as one non-negative signed 64-bit `Gold` scalar. This behavior is classified **STRONGLY CORRELATED** because the same field is persisted and consumed consistently by quest price checks, NPC rewards, direct character transfers, death corpse creation, and corpse recovery.

All modified Character gold mutations now use one checked runtime contract. Credits reject negative amounts and overflow. Debits reject negative amounts and insufficient funds. Two-character transfers compute both post-states, verify exact conservation of the combined balance, and commit through the existing atomic character transaction.

## Death ownership

On player death, the validated carried balance is escrowed into the corpse and the character balance is set to zero in the same saved transition. Recovery credits the corpse balance through the same checked contract and clears corpse gold through the item engine.

## Capacity quarantine

A pre-existing runtime branch interpreted arbitrary item-instance metadata named `capacity` as a maximum direct-child item count. No decoded source established that unit or rule. The branch has been removed from execution. Capacity metadata remains preserved as evidence but inert until native semantics are recovered.

## Remaining unknowns

Native denominations, coin weight, bank conversion, carrying-capacity units, encumbrance bands, and overflow destinations remain `UNKNOWN` and are not invented.
