# Handoff — Runtime-0013A Class & Race Engine

Use this ZIP as the new cumulative baseline after Runtime-0012.

## What changed

Added a dedicated class/race runtime layer:

- Loads all canonical classes and races
- Normalizes class and race IDs
- Validates character stats against race bounds
- Builds class/race profiles for downstream systems
- Preserves race ability records without inventing behavior
- Emits trace/provenance data for formulas and validation

## Verification

Run:

```bash
python Sim/run_verification_suite.py
```

Expected result:

- Exit code `0`
- `validate_class_race_engine`: `PASS`
- Remaining exact classic class/race formulas: `UNKNOWN by design`

## Next recommended milestone

Runtime-0013 — Persistence / Save-Load, unless you want to deepen classes/races first with a second pass for restrictions and character creation UI.
