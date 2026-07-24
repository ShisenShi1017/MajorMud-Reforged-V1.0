# Remaining Systems 0006 — Environmental Hazards

## Executable reconstruction

`RuntimeEnvironmentalHazardService` now owns hazard definitions, enabled state, room occupancy registration, entry/exit/periodic phases, tick scheduling, actor eligibility, exact protection identifiers, typed downstream requests, state validation, and optional world-state persistence.

## Supported policy families

The runtime models darkness/light, fire, water/drowning, falling, lava, poison/disease, cold/heat, air and terrain restrictions, slippery or forced movement, regeneration penalties, and spell restrictions without asserting that every family exists in Classic data.

## Ownership

Rooms own movement and occupancy. Combat and Spells own damage, status, resistance, and spell-effect resolution. Character and NPC systems own resources and protection values. Environmental Hazards owns lifecycle, scheduling, eligibility, protection routing, and request generation.

## Evidence-safe limits

No native damage formula, resistance roll, tick duration, message, or room-field meaning was invented. Exact identifier matches are required for protection. Missing request identifiers remain presentation-only.

## Validation

`--smoke-environmental-hazards` covers entry requests, equipment protection, periodic processing, visibility routing, enable/disable policy, and state invariants.

## Final-native dependencies

Classic record mapping, timing, formula arithmetic, protection precedence, NPC eligibility, message rendering, persistence/reset semantics, and weather/day-night coupling remain assigned to the final native pass.
