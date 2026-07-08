# Runtime-0013A — Class & Race Engine

## Purpose

This milestone adds a dedicated runtime adapter for playable classes and races. It converts canonical `/Data/classes.json` and `/Data/races.json` into runtime-safe objects that can be consumed by character creation, equipment restrictions, leveling, regeneration, combat, spell eligibility, trainers, and future persistence.

## Included

- `Runtime/class_race_engine.py`
- `Sim/validators/validate_class_race_engine.py`
- `Reports/class_race_engine_report_0013.json`
- Registry integration into the full verification suite

## Evidence policy

This milestone does **not** invent hidden MajorMUD class/race mechanics. It promotes only data-backed facts already present in `/Data`:

- Class IDs and names
- Class HP/mana-per-level values present in data
- Race legacy numbers
- Race min/max stat bounds
- Race HP bonus, CP cost, and EXP modifier fields
- Race ability records as raw evidence-preserving payloads

Exact classic-only behavior remains `UNKNOWN` until verified by evidence.

## Runtime API

Primary entry point:

```python
from Runtime.class_race_engine import ClassRaceEngine

engine = ClassRaceEngine("Data")
profile = engine.build_profile(
    character_name="RuntimeTester",
    class_id="warrior",
    race_id="dwarf",
    level=1,
    stats={"str": 50, "int": 30, "wil": 50, "agl": 30, "hea": 50, "cha": 30},
)
```

The profile includes:

- selected class/race IDs
- level
- stats
- class HP/mana-per-level data
- race HP/CP/EXP data
- race ability payloads
- validation warnings
- trace/provenance entries

## Remaining work

- Exact class progression formulas
- Class-specific passive abilities
- Race-specific passive interpretation
- Skill acquisition rules
- Spell-learning rules
- Equipment restrictions by class/race
- Class/race special abilities
- Validation against MMExplorer/MBBSEmu/runtime evidence
