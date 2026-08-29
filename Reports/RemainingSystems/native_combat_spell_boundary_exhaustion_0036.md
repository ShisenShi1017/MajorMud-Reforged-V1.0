# Native Combat / Spell Boundary Exhaustion — Pass 59 / Update 0330

Pass 59 promotes `combat-spells.native-boundary` from UNKNOWN to STRONG. This is an ownership/boundary closure, not a claim that every remaining native formula or branch is exact.

## Closed boundary

- The managed/non-native reconstruction already classifies all **1,350 canonical spells** and **111 runtime status effects**.
- Retained WCCMMUD and WBBMMUD each expose **592 exports** and the same selected **102 combat/spell owner anchors**: 45 attack/combat, 53 spell/cast, and 4 ability-lookup anchors.
- Dedicated native owners are therefore bounded for attack/kill/autocombat, target/energy flow, spell record IO, cast/use dispatch, monster/room spell delivery, ability lookup, and spell upkeep/termination.

## Still fail-closed

Exact cross-formula arithmetic/order, cast branch priority, target ordering, resource spend/refund order, resistance/save precedence, status collision order, multi-target partial failure behavior, and combat/spell RNG draw ordering remain unresolved. Reforged atomic rollback, deterministic ordering, and random-stream splitting are compatibility policy and receive no Classic parity credit.

## Message 8480

Nahr NPC 1006 remains UNKNOWN. The MDB supplies exact hit prose, but the native 8480 formatter consumes separate caller-owned miss-verb and weapon/body-part arguments. Those pairs are absent from the updater record/public reference and cannot be inferred safely.
