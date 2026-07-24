# Character Status Effects and Timers Phase 2 — CodeUpdate0150

## Promoted runtime behavior

`RuntimeSpellState.ActiveEffects` remains the authoritative owner. Successful casts create independent effect instances; there is no implicit same-spell refresh or replacement in the reconstructed runtime. Effects coexist by unique `EffectId` until UTC expiration or explicit dispel.

Character synchronization now projects spell ID, caster ownership, application and expiration timestamps, deterministic projection ordinal, replacement policy, and timer owner. Projection order is `AppliedAtUtc`, then `EffectId` using ordinal-ignore-case comparison.

Synchronization removes stale spell projections after expiration or dispel but preserves unrelated character-owned statuses and timers. This closes the previous overwrite defect where synchronizing spell effects could discard non-spell semantic state.

## Persistence and reconnect

World spell state is normalized before save and after load. Known spells, active effects, and resource owners have deterministic serialization order. The existing temporary-file replacement remains the atomic save boundary. Character save version advances to 6; versions 1 through 5 remain readable.

## Evidence classification

All promoted behavior is **PROVEN** from the active runtime ownership, cast, expiration, dispel, persistence, and synchronization paths. Native named status categories, priority/replacement bitmasks, periodic formulas, and death/login reset matrices remain **UNKNOWN** for the final native pass.
