# Server Runtime Validation 0009

This folder records the user-supplied `ServerRuntimeValidation0009_NoDotNet_NpcLifecycleCombatStatePatch_Handoff.zip` as a folded CodeUpdate-0063 input.

## Scope

Validation0009 applies to the no-.NET simulator surface under `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`.

It does not activate Server gameplay, sockets, sessions, scheduler execution, world simulation, combat services, AI, or runtime rules.

## Applied Simulator Changes

- Added explicit simulator combat lifecycle state.
- Added `*Combat Engaged*` output when a player-started simulator attack opens a target engagement.
- Added `*Combat Off*` output when the simulator target is defeated and combat state clears.
- Changed simulator respawn behavior from elapsed-minute modulo refresh to a death-triggered expected-model respawn due minute.
- Added canonical `/Data/monsters.json` fallback use for simulator monster HP, damage, XP, and currency where a canonical monster resolves.

## Validation Handoff Result

The uploaded handoff reported:

- Smoke test: PASS.
- Eligible single-mob respawn rooms checked: 10.
- Lifecycle checks run: 10.
- Lifecycle PASS: 10.
- Lifecycle FAIL: 0.
- Autonomous aggro implemented: false.

These are handoff-reported results from the uploaded package. CodeUpdate-0063 local verification records the tests actually run in this repository.

## Remaining Boundary

Autonomous NPC aggro, assist behavior, initiative, attack timing, targeting, party combat AI, formulas, and live Worldgroup parity remain unimplemented and uncertified.
