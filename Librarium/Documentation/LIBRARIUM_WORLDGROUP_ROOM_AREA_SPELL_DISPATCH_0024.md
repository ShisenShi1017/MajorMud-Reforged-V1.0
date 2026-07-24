# Worldgroup Room/Area Spell Dispatch — Update0024

## Proven boundary

`wccmmud.dll!0x00449232-0x00449BF0` is the four-argument room/area-target spell and power dispatcher. It accepts the definition record, caster Worldgroup session index, room/area handle, and invocation mode.

The dispatcher resolves the caster, class/archetype context, and room record; accepts definition types 6 and 7; applies caster-state and eligibility gates; checks resource, secondary-resource, and level requirements; and processes at most ten effect slots.

## Chance and cost ordering

A bounded `0..100` roll is compared with a threshold derived from caster field `+0x604` and definition field `+0xC8`, normally capped at 98. Successful execution subtracts the full definition cost and positive secondary requirement before the effect loop. A failed roll subtracts the full resource cost and half of the signed secondary requirement, emits failure output, and broadcasts the failure to the room.

## Effect loop

Effect IDs are read from `definition + 0xD8 + index*2`; effect values come from `definition + 0xA8 + index*2` or a shared randomized magnitude. The direct switch covers IDs `0..53` with separately tested IDs `105`, `106`, `122`, `143`, `150`, `153`, `162`, and `178`.

Effect 143 reaches room mutation helper `0x0046C241` with the caster room coordinates and effect value. Effects 162 and 178 can use generic helper `0x0043FC13` with selector 3. Other numeric effects remain unlabeled until independent MajorMUD record evidence proves their semantics.

## Output ownership

Caster-facing output is flushed through `0x0046B170`. Room-scoped output uses the previously recovered recipient dispatcher `0x0046B010`.
