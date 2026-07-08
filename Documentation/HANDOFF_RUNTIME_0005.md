# Handoff — Runtime-0005 Regeneration

## Current milestone

Runtime-0005 adds HP/Mana/CP and regeneration calculator foundations.

## What passed

- Data load
- Reference integrity
- Formula engine
- Schema adapters
- Character snapshot/equipment
- EXP/leveling
- HP/Mana/CP + regeneration

## What remains unknown by design

- Exact classic HP formula
- Exact classic mana formula
- Exact classic CP formula
- Exact classic regeneration timing
- Exact classic resting/combat regen modifiers
- Exact poison/bleed/status regen interactions

## Next recommended milestone

Runtime-0006 — Movement Runtime Foundation

Suggested scope:

- Load room graph
- Create offline room-state model
- Move character snapshots between rooms
- Validate normal exits
- Preserve special/hidden/locked exits as evidence-gated blockers
