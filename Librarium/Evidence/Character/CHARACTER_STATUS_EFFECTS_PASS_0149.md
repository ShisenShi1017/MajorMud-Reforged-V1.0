# Character Status Effects and Timers — CodeUpdate0149

## Promoted behavior

The authoritative runtime status owner is `RuntimeSpellState.ActiveEffects`. Each effect has a unique effect ID, spell ID, caster, target, generated magnitude bounds, duration, application time, and optional UTC expiration.

Character-targeted effects are now synchronized into `RuntimeCharacterSemanticState.StatusEffects` and matching expiration timers. Successful casts add the effect, `Expire` removes due effects, and explicit dispel removes the selected effect ID. The world power repository remains the authoritative reconnect source; the Character ledger provides character-local persistence, validation, and auditability.

The promoted stacking statement is deliberately narrow: independent effect instances coexist because they have unique effect IDs. No same-spell refresh, category priority, poison, blind, fear, curse, or removal-operation meaning is inferred.

## Save compatibility

Synchronized status state advances the Character save contract to version 5. Versions 1 through 4 remain readable.

## Native dependencies retained

Named status semantics, periodic formulas, refresh/priority rules, `RemovesSpell` relationships, death/logout reset rules, and native bitfields remain blocked for the final native pass.
