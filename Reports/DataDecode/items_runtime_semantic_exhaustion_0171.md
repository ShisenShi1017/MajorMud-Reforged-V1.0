# Items Runtime Semantic Exhaustion — CodeUpdate0171

## Executable advancement

CodeUpdate0171 adds atomic destination-stack reconciliation after quantity transfers. Compatible stacks are matched using the established full merge identity, filled deterministically in instance-ID order, and capped by evidence-projected `maximum_stack` metadata.

A fully absorbed transferred instance is removed while the pre-existing destination identity survives. When capacity is insufficient, the transferred identity survives with the overflow quantity. No quantity is silently discarded and all mutations remain inside the existing rollback boundary.

## Validation advancement

Persistent item validation now rejects malformed non-positive `maximum_stack` metadata and stack quantities exceeding a projected maximum.

## Evidence boundary

This is strongly correlated Reforged compatibility infrastructure. Classic activation remains fail-closed because the current generated Classic item projection contains no explicitly stackable definitions and no authoritative native maximum-stack values.

## Remaining native dependencies

- Classic stackable flag mapping.
- Native maximum stack quantities.
- Native destination-stack selection order.
- Native overflow rejection, messaging, and transaction sequencing.
- Quantity-sensitive limited-item counting and pricing.
