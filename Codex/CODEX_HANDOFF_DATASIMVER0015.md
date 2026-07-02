# Codex Handoff — DataSimVer0015

Continue from `MajorMUD-Reforged-DataSimVer0015-Cumulative.zip`.

Authoritative source remains `/Data` inside the cumulative package.

DataSimVer0015 completed a real combat/status/spell-state expected-model pass from `Data/monsters.json` and `Data/spells.json`.

Key measured results:
- Monsters analyzed: 1100
- Monster attack rows: 2215
- Spells analyzed: 1350
- Status/duration spell clues: 543
- Combat attempts logged: 240
- Combat expected successes/failures: 173 / 67
- Spell cast attempts logged: 1500
- Spell expected successes/failures: 1496 / 4

Rules:
- `/Data` is unchanged.
- Do not promote formulas until verified against original runtime or MBBSEmu.
- Keep user-added DataExtensions overlay separate from legacy `/Data`.

Recommended next pass: DataSimVer0016 — monster ecology, spawn pressure, loot/death resolution, and encounter routing.
