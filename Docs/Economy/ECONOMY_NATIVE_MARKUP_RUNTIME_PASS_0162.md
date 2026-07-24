# Economy CodeUpdate0162 — Native Markup Pass

## Implemented

- Bound all 178 decoded WCC shop markup fields to canonical providers.
- Classified markup domains for merchandise, trainer, spell-service and other providers.
- Established `100` as the neutral 1.00x field value from the cross-record pattern.
- Converted whole-percent markup to basis points with checked arithmetic.
- Corrected service-cost projection from additive `100% + markup` behavior to multiplicative `markup%` behavior.
- Added deterministic rounding, validation, evidence, provenance and Librarium relationships.

## Native boundary

The field encoding and multiplicative interpretation are strongly correlated and active. The original executable position of markup relative to level/rank adjustments, discounts, surcharges, minimum charges and final rounding remains unproven. Native-exact service calculation therefore remains fail-closed until that ordering is traced.

## Ownership and persistence

Economy owns markup conversion and checked arithmetic. Commerce or service runtimes own stage placement according to their selected verified policy. Provider markup definitions are immutable catalog data and do not add save-state fields.
