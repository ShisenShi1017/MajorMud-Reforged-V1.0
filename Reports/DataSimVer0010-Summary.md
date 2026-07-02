# DataSimVer0010 Summary — Full Virtual Player Pathing

## Objective
Run a full expected-model player-state traversal using reusable virtual characters. This pass tests how access changes as level, inventory, spell access, and progress flags change.

## What Was Simulated
- Six virtual character profiles, from Level 1 baseline through diagnostic all-state.
- Directional movement across all known room exits.
- Expected-model blocking for likely level, item/key, quest/flag, spell, command, and engine-handled exit clues.
- Per-profile reachability and blocked-transition counts.

## Discoveries
- Character state materially changes expected reachability and must remain a first-class simulator concept.
- Many apparent endpoint/path gaps are better treated as conditional access hypotheses rather than data failures.
- Diagnostic all-state traversal gives a useful upper bound for graph coverage, but it is not original runtime proof.
- The simulator should compare future reverse-engineered parser/flag findings against this expected-model matrix.

## Statistics
- Rooms analyzed: 1,246
- Room exits/action edges analyzed per profile: 3,022
- Virtual character profiles: 6
- Player-state transition rows: 10,852
- Command/action clue rooms: 822
- Candidate key/item inventory objects available to profiles: 372
- Result categories: {'allowed_expected_model': 7933, 'invalid_destination': 731, 'blocked_item_or_engine_gate_hypothesis': 18, 'blocked_flag_hypothesis': 31, 'blocked_spell_hypothesis': 25, 'blocked_level_hypothesis': 34, 'allowed_diagnostic': 2080}

## Changes
- `/Data`: Unchanged.
- Room endpoints promoted: 0.
- Special-exit semantics promoted: 0.
- Added player-state traversal evidence, coverage JSON, command/action catalog, and next-window handoff.

## Remaining Unknowns
- Exact original command parser precedence.
- Exact item/key matching rules.
- Actual quest/flag bit fields and evaluation order.
- Exact spell travel behavior.
- Runtime handling of nonzero exit_type values.

## Impact
DataSimVer has moved from structural validation into behavior reconstruction. Current simulator results remain labeled expected-model behavior until compared against reverse-engineered engine/MBBSEmu evidence.

## Next Pass
DataSimVer0011 should start the MBBSEmu/original-engine comparison track once reverse-engineered notes are available, or otherwise focus on spell/travel behavior discovery.

## Session Health
Recommendation: start a new window after this pass. Continue from this ZIP and the Codex handoff.
