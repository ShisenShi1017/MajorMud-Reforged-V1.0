# DataSimVer0021 Report — Monster Respawn Scheduler Model

## Objective
Document and simulate the monster respawn scheduling model supplied by user runtime knowledge while preserving evidence-first reconstruction rules.

## What Was Simulated
- Standard monster respawn as an approximately 15-minute global scheduler.
- Explicit monster respawn timers as death-triggered per-monster countdowns.
- Explicit timers continuing through daily cleanup/reset.
- Separation between cleanup/reset and monster respawn scheduling.

## Discoveries
No new direct runtime evidence was introduced. The model is consistent with existing timer-like room and monster fields, but consistency is not proof of original runtime behavior.

## Statistics
- Monster records analyzed: 1100
- Monster timer-like field candidates: 0
- Room records analyzed: 1246
- Room timer-like field candidates: 1246
- /Data changed: no

## Changes
Added DataSimVer0021 simulator, evidence, validation, documentation, and codex artifacts. Updated EngineKnowledge.md with a hypothesis-only respawn scheduler note.

## Remaining Unknowns
- Exact global respawn interval and whether it is hardcoded/configurable.
- Exact eligibility checks for normal monsters.
- Exact timer queue implementation for explicit monster timers.
- Whether quest/boss monsters bypass or alter either scheduler.
- Interaction with room occupancy, party state, and players present in room.

## Next Pass
DataSimVer0022 should focus on quest/boss/unique monster persistence and occupancy rules.

## Session Health
Healthy. No sequence conflict. No evidence conflict. No new window required.
