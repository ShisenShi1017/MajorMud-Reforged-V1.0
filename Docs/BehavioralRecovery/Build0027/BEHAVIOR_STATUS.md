# Behavior Status

## High-confidence / data-backed

- Item, monster, room, shop, spell, skill, race, class, and quest catalogs.
- Room/shop/spawn/static relationships where supported by imported database/repo data.
- Race/class/equipment restrictions where encoded in data/source.
- World topology and static entity placement, subject to final repo audit.

## Expected-model / simulator-projection only

- Archetype/hybrid scenario expectations from Build0024.
- Sensitivity matrices from Build0025.
- Formula-shape projections from Build0025/0026.

These are useful design/test targets but are not proof of original runtime formulas.

## Still unresolved / runtime_required unless source-backed hooks are recovered

- Exact hit chance.
- Damage variance.
- Critical-hit chance and multiplier.
- RNG behavior and seeding.
- Mitigation ordering if not explicitly recovered.
- Spell fizzle/resist/interrupt behavior.
- Healing scaling if not source-backed.
- Sneak/backstab success probabilities and failure effects.
- Training cost formula.
- Banking edge behavior.
- Dynamic shop quirks.
- Tick timing, regeneration timing, and event scheduling.
- Dynamic quest/event state mutation edge cases.
