# Architecture Vision Update - Unified Calculation Engine

Date: 2026-07-02T20:57:54.495308+00:00

## Core Principle
The Librarium uses one shared calculation engine. All calculators operate on the same active build and world rules.

### Build Sources
- Import Current Character
- Open Saved Build
- Create New Build

### Shared Inputs
Character:
- Race
- Class
- Level
- Stats
- Skills
- Learned spells
- Buffs/Debuffs

Equipment:
- Equipped gear
- Inventory selections
- Enchantments
- Set bonuses

Environment:
- Monster
- Events
- Room modifiers
- AI modifiers

World Rules:
- Classic
- Reforged Custom
- AI Layer
- Pure AI
- Custom formulas

### Shared Outputs
- Damage
- Defense
- Magic
- Resistances
- Carry weight
- XP
- Loot
- Combat simulation
- Equipment comparison
- Build planning

Every change immediately updates every analysis view.

## Suggestions
- Dependency viewer explaining why values changed.
- Live comparison mode.
- Progression forecasting.
- Upgrade recommendations.
- Compare the same build under multiple world rules.
