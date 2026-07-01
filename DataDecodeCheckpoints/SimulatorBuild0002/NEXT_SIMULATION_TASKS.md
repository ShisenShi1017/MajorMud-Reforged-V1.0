# Recommended Next Simulation Work

## SimulatorBuild0003 — Spawn and Timer Engine

Implement deterministic expected-model timers for configured monster/boss respawn data. Keep all timing marked as configured/expected unless observed in Worldgroup.

Deliverables:
- Spawn state manager
- Room population tick/update loop
- Boss respawn handling from configured data
- Spawn trace log
- Spawn unknowns report
- CLI commands for `tick`, `spawn-status`, `simulate-spawns`, and `export-spawn-trace`

## SimulatorBuild0004 — Shop/Loot Interaction Engine

Implement buy/sell/inventory/drop simulation using verified shop and loot matrices.

## SimulatorBuild0005 — NPC/Quest Interaction Shell

Use RuntimePrep0006 and quest supporting documentation to expose dialogue and quest-candidate flows without asserting unresolved script behavior.

## SimulatorBuild0006 — Combat Prototype

Implement basic data-driven combat with explicit placeholder handling for unverified formulas and AI choices.
