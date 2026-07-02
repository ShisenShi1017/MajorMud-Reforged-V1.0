# DataSimVer0015 — Combat, Status Effect, and Spell-State Simulation

## Objective
Build an evidence-based expected behavior model for combat encounters, monster attacks, spell-triggered hit effects, and timed/status spell state using the authoritative DataSimVer0014 cumulative package.

## What Was Simulated
- Monster combat rows were extracted from `Data/monsters.json`.
- Spell/status rows were extracted from `Data/spells.json`.
- Six virtual player profiles were matched against a measured sample of monsters across level bands.
- Spell casts were checked against profile level, mana, and energy constraints.
- Attack `hit_spell` references were resolved against the spell catalog.

## Discoveries
- Monsters analyzed: **1100**.
- Monster attack rows identified: **2215**.
- Attack rows referencing hit spells: **485**.
- Resolved attack hit-spell references: **485**.
- Unresolved attack hit-spell references: **0**.
- Spells analyzed: **1350**.
- Spells with non-zero duration/status behavior: **543**.
- Damage or drain-like spells: **1030**.
- Summon-like spells: **88**.
- Remove/cleanse/counter-spell clues: **107**.
- Buff/debuff-like spell clues: **207**.

## Statistics
- Combat profiles: **6**.
- Monster sample used for encounter simulation: **40**.
- Combat attempts logged: **240**.
- Expected-model combat successes: **173**.
- Expected-model combat failures: **67**.
- Spell cast attempts logged: **1500**.
- Expected-model spell successes: **1496**.
- Expected-model spell failures: **4**.

## Changes
- `/Data`: **unchanged**.
- Room endpoints promoted: **0**.
- Special-exit semantics promoted: **0**.
- Existing `/DataExtensions` overlay from DataSimVer0013 was preserved separately.

## Remaining Unknowns
- Exact original hit roll formula.
- Exact initiative/energy tick timing.
- Exact AC, DR, MR, dodge, and resist formulas.
- How attack percent rows are scheduled by the original engine.
- Whether some spell rows are player-castable, monster-only, item-triggered, or system-only.
- Runtime stacking/refresh/removal behavior for timed effects.

## Next Pass
DataSimVer0016 should focus on monster ecology, spawn pressure, loot/death resolution, and encounter routing using the combat-state model produced here.

## Session Health
Healthy. This pass used the real 0014 cumulative ZIP and generated measured artifacts. No runtime behavior was promoted into `/Data`.
