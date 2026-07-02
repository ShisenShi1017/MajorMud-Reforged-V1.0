# Validation0005 — Routine-Level Promotion Readiness Review

## Objective
Identify MajorMUD disassembly procedure neighborhoods and MBBSEmu host files that are strong candidates for promotion review, while preserving the no-guessing `/Data` policy.

## What was analyzed
- MajorMUD disassembly: `MBBSEmu.Disassemblies-main/MajorMUD/WCCMMUD.DLL.asm`
- MajorMUD procedures detected: 802
- Far procedures: 800
- Near procedures: 2
- MBBSEmu C# files with host lifecycle/storage/timer signals: 336
- `/Data` files hash-checked: 92

## Discoveries
- High-signal routine clusters now exist for cleanup/search/hidden discovery, respawn/reset/timer behavior, combat, spells/status effects, quest/persistence, and boss/unique lifecycle candidates.
- MBBSEmu host files provide strong supporting context for module polling, RTKICK, event/timer handling, user/channel state, and Btrieve-style persistence.
- Evidence is now strong enough to promote some concepts into **EngineKnowledge/simulator documentation**, but not into `/Data` because exact field binding is still incomplete.

## Candidate procedure counts
- cleanup_search_hidden: 88
- respawn_reset_timer: 92
- combat_attack_damage: 564
- spell_status: 147
- quest_persistence: 666
- boss_unique_lifecycle: 66


## Promotion review outcome
Approved for EngineKnowledge/simulator documentation only:
- Host polling / RTKICK scheduling exists.
- Search command and hidden discovery path exist.

Still not approved for `/Data` promotion:
- Exact cleanup delete/hide flags.
- Exact respawn timing formula and timer ownership.
- Exact combat formulas.
- Exact quest variable storage/transitions.
- Boss/unique lifecycle flags.

## Statistics
- Promotion candidates reviewed: 6
- `/Data` promotions approved: 0
- Engine/documentation promotions approved: 2
- Previous ZIPs included: No

## Remaining unknowns
- Exact data fields for hide-on-cleanup vs destroy-on-cleanup.
- Exact search formula and stat weighting.
- Exact normal monster global respawn implementation.
- Exact explicit death-start timer implementation.
- Quest persistence scope and save/load record layout.
- Boss/unique spawn exclusivity rules.

## Next validation pass
Validation0006 should map specific high-signal procedures into named behavior routines and attempt field binding using MDB extractor output and current `/Data` records.
