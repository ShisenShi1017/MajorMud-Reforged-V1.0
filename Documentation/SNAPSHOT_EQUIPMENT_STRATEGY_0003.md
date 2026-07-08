# Snapshot and Equipment Strategy 0003

## Design rule
Character snapshots are offline runtime objects used by simulators and validators. They are not live player saves yet.

## Snapshot responsibilities
A snapshot stores:
- name
- race id
- class id
- level
- base stats
- skills
- equipment ids
- inventory ids
- provenance

## Equipment calculator responsibilities
The calculator aggregates explicit fields from canonical item records. It returns a trace so every result can be inspected.

## Provenance rule
All Runtime-0003 calculations are tagged:

`classic_accuracy = not_classic_claimed`

Classic claims require later evidence from MMExplorer, MBBSEmu, MegaMUD, logs, screenshots, or verified formula derivation.

## Why this matters
This gives the runtime a safe foundation for later combat, EXP, spell, and shop systems without mixing verified classic behavior with Reforged formulas.
