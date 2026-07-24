# Combat & Spells Runtime Update 0156

## Executable runtime advance

This pass adds an evidence-gated active spell-effect lifecycle. The runtime now owns active effect identity, monotonic world time, deterministic periodic tick scheduling, expiration, explicit removal, death-event removal, and four stack-policy branches.

Applications fail closed when effect timing, stack behavior, maximum stacks, evidence, canonical references, or safe fallback are unresolved. No production meaning is assigned to `spell.1304`.

## Deterministic ordering

Due effects are processed by next tick, expiration tick, then canonical effect ID. Periodic ticks are emitted only when the due tick is strictly earlier than expiration. Repeated terminal transitions are rejected.

## Ownership

The effect lifecycle owns effect instances and their timers. Damage or healing mutations emitted by periodic ticks remain owned by the combat mutation boundary. Combat death processing supplies target-death events; persistence remains disabled unless promoted policy proves it.

## Native dependencies retained

Exact production status meanings, duration values, stacking behavior, persistence, dispel ordering, and same-tick ordering relative to death, movement, regeneration, and casting remain explicit native dependencies.
