# DataSimVer0008 Summary — NPC, Shop, Trainer, and Service Behavior Discovery

## Objective
Model service and NPC-related behavior as expected data-driven behavior in a Worldgroup/MajorMUD-style interpreter.

## What Was Simulated / Classified
- Shop room links and level/class restrictions.
- Shop inventory item references and regeneration fields.
- Trainer room references.
- Monster/NPC spawn references from room data.
- Monster behavior flags including follow/charm/undead/spells/attacks.
- Service-room clues from room names/descriptions.

## Discoveries / Statistics
- Shops analyzed: **178**
- Shop inventory rows: **3550**
- Invalid shop locations: **30**
- Invalid shop inventory references: **2346**
- Trainers analyzed: **13**
- Invalid trainer room references: **0**
- Monsters analyzed: **1100**
- Monster ability rows: **2293**
- Spawn rooms identified: **962**
- Invalid monster spawn references: **7**
- Service room clues: **436**

## Changes
- `/Data`: **Unchanged**
- Room endpoints promoted: **0**
- Special-exit semantics promoted: **0**
- Evidence added under `/Evidence/DataSimVer0008`, `/Validation/DataSimVer0008`, and `/Simulator/DataSimVer/0008`.

## Remaining Unknowns
- Exact original shop command parser behavior.
- Trainer class/skill validation order.
- NPC aggression/follow timing and random behavior.
- How Worldgroup runtime ticks map to shop regeneration and monster respawn.
- MBBSEmu/original module comparison still needed for runtime proof.

## Impact
This pass starts connecting virtual character state to services and NPC-driven rooms. It improves the expected-model interpreter without promoting data or claiming original runtime proof.

## Next Pass
DataSimVer0009 should focus on quest/flag behavior discovery and compare quest clues against room, item, NPC, and command evidence.

## Session Health
✅ Continue in current window. New window not required yet, but consider switching after DataSimVer0010 with the cumulative ZIP and handoff.
