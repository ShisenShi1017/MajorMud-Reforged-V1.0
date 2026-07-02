# Recent Findings — Worldgroup/MajorMUD Data-Driven Simulation Strategy

## Working model

The project should treat MajorMUD as a Worldgroup/MajorBBS-era module: a relatively small engine/interpreter processing structured world data. The `/Data` folder remains the authoritative game content, while simulator passes reconstruct expected behavior by applying increasingly accurate engine rules.

## User-confirmed behavior categories

Movement and access should include:

- Ten direction commands: north, south, east, west, northeast, northwest, southeast, southwest, up, down.
- Abbreviations expected for dial-up/BBS command parsers: n, s, e, w, ne, nw, se, sw, u, d.
- Room-specific text commands such as enter, climb, crawl, pull, push, open, unlock, read, use, search, and similar verb-first commands.
- Key/item gates.
- Door/lock state gates.
- Character-level gates.
- Quest/flag gates where encoded by data or later verified by engine reverse engineering.
- Special/unknown exits documented only until semantics are proven.

## Why this matters

Earlier static validation correctly established a trustworthy `/Data` baseline. The next phase is behavior discovery. DataSimVer should therefore be treated less like a data linter and more like a compact expected-runtime interpreter.

## Reverse-engineering comparison plan

The user will reverse-engineer the original MajorMUD/Worldgroup behavior on a local PC and may compare against MBBSEmu findings. Future confirmed engine details should be added to an EngineKnowledge document with provenance:

- Confirmed by original module disassembly/debugging.
- Confirmed by MBBSEmu behavior/source/documentation.
- Confirmed by data correlation.
- Inferred by simulator only.

Only confirmed or strongly evidenced engine rules should change simulator behavior. `/Data` must remain unchanged unless direct evidence supports a data correction.

## Current simulator stance

DataSimVer0005 harvests likely command/parser clues from data so future passes can test small, targeted action sets instead of brute-forcing all commands across all rooms. This should reduce freezes and make reports easier to audit.
