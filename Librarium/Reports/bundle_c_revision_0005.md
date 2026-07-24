# Bundle C Revision 0005 — Phase C4 Items

## DONE
Phase C4 item authority exhaustion completed. Generated Classic item definitions are consumed by `RuntimeItemCatalog`; instances, stacks, charges, durability, destruction, quest grants/consumption, portable containers, keys, cleanup deletion, hidden-search interaction, limited-item release and item-state persistence retain their existing single runtime owners. A repository-enforced audit now prevents these decoded fields from regressing to generated-only status.

## VERIFIED
- 1,950 generated Classic item definitions load into the runtime catalog.
- Item identity, type, gettable, container, consumable, stackable, default charges, weight and cleanup-delete projection have runtime owners.
- Consumable quantity and stack depletion are atomic.
- Charges and recharge limits are atomic.
- Non-empty containers cannot be consumed or destroyed through normal consumption.
- Keys are consumed as access evidence by the world-container authority.
- Quest item grant, consumption and cleanup are persisted item-instance operations.
- `del@cleanup` is represented by `DeleteAtCleanup` and consumed by Classic cleanup.
- Hidden room objects remain governed by the existing per-player hidden-search authority.
- Item state, metadata, charges, quantities, durability, locations and corpse/container hierarchy persist through `RuntimeItemState`.

## STRONGLY CORROBORATED
- LearnSp remains owned by `RuntimeCharacterEligibilityAuthority`, sourced from decoded item ability 42 relationships.
- Item decay is only promoted where an explicit lifecycle/expiry record exists; no universal decay behavior is invented.

## DECODED BUT NOT MAPPED
None among Verified or Strongly Corroborated C4 fields.

## CONFLICTS
None introduced.

## TRUE UNKNOWN
Unresolved native-only item effects without a verified relationship remain quarantined for the final native pass.

## Duplicate Paths Removed
No exact duplicate item execution owner was removed; existing specialized authorities were retained and audited rather than replaced.
