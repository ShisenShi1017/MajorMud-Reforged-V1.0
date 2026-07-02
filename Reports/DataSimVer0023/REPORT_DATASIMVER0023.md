# DataSimVer0023 Report — Quest/Boss Respawn and Occupancy Model

## Objective
Extend the simulator documentation around quest monster persistence, boss/unique monster respawn behavior, and room occupancy rules during respawn while preserving `/Data` as evidence-only.

## What Was Simulated
- Boss/unique monsters as candidates for explicit timers, quest gates, or special persistence rules.
- Quest monsters as gated by quest state rather than assumed normal creature respawn.
- Room occupancy checks as a likely duplicate-prevention step during respawn.
- Separation between cleanup flags and respawn flags.

## Discoveries
No direct executable/runtime evidence was introduced. The pass found candidate records and fields that are useful for later verification, but no behavior was promoted.

## Statistics
- Rooms scanned: 1246
- Monsters scanned: 1100
- Items scanned: 1950
- Quests scanned: 0
- Monster explicit timer candidates: 0
- Boss/unique monster candidates: 132
- Quest monster persistence candidates: 67
- Spawn room candidates: 1246
- Room occupancy candidates: 0
- Quest item persistence candidates: 1950

## Changes
Added DataSimVer0023 simulator, evidence, validation, documentation, and codex artifacts. Updated EngineKnowledge.md. `/Data` was not modified.

## Remaining Unknowns
- Exact quest-state flags that block or allow monster respawn.
- Whether unique/boss monsters use normal scheduler, explicit timers, or custom scripts.
- Exact duplicate-prevention/occupancy rule for respawn.
- Whether players present in a room delay or block respawn.
- Exact cleanup flag vs respawn flag schema.

## Next Pass
DataSimVer0024 should focus on player-present room behavior, duplicate spawn prevention, and event ordering between cleanup, respawn, and quest gates.

## Session Health
Healthy. No sequence conflict. No new window required.
