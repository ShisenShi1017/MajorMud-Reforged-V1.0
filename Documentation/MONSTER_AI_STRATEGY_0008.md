# Runtime-0008 Monster AI Runtime Foundation

Runtime-0008 adds the first monster AI runtime layer.

## Scope

Implemented:

- Monster behavior profile indexing from canonical `/Data/monsters.json`
- Legacy-number and runtime-id monster lookup
- Follow-candidate classification from `behavior.follow`
- Aggro decision surface placeholder
- Assist candidate surface using canonical `groupId`
- Pack candidate surface using canonical `groupId`
- Traceable decision output for all AI calls
- Validation coverage for profile creation and decision scaffolds

## Evidence rule

This milestone does **not** claim verified classic AI behavior. It preserves and exposes the available canonical behavior fields so future evidence can bind exact mechanics.

Classic-unknown behavior retained as UNKNOWN:

- Exact aggro triggers
- Aggro probability/timing
- Follow roll timing
- Adjacent-room pursuit
- Assist call timing/range
- Pack attack coordination
- Target switching
- Fleeing
- Spell/attack turn choice

## Runtime modes

The default remains `classic_unverified`. Any future Reforged-specific AI formulas must be added as separate mode-tagged behavior and must not overwrite classic-unverified surfaces.
