# Bundle D Revision 0024 — Runtime Field Classification

## Correction

The Bundle D World Authority Audit previously treated every nested JSON property as an executable gameplay field. That incorrectly classified compiler metadata, decode provenance, source offsets, evidence records, validation notes, and structural containers as `Decoded but not mapped`.

Revision 0024 separates:

- **Executable gameplay fields** — require a runtime owner, live consumer, and validation owner.
- **Decode metadata** — retained for Librarium/provenance and explicitly requires no runtime consumer.

## Runtime mappings added

The audit now recognizes existing live consumers for placed room items, hidden item definitions, imported room environment fields, required items, tolls, damage/spell effects, command aliases, reciprocal exit state, action ordering, class/race/alignment/ability restrictions, picklock/strength alternatives, automatic routing, fallback behavior, and persisted state identity.

## Closure behavior

Only fields still classified as `Decoded but not mapped` participate in the executable gameplay disconnect list. `Decode metadata` is reported separately and does not block Bundle D completion.
