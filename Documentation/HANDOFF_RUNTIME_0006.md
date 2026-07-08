# Handoff Runtime-0006 — Movement Runtime Foundation

This cumulative milestone builds on Runtime-0005 and adds `Runtime/movement.py` plus `Sim/validators/validate_movement.py`.

## What Changed

- Added `MovementRuntime`.
- Added movement result and trace dataclasses.
- Added room and exit indexing.
- Added safe traversal for normal/open exits.
- Added preservation of unresolved special exits.
- Added validation suite coverage for movement.

## Verification

Run from the package root:

```bash
python Sim/run_verification_suite.py
```

Expected high-level result:

- Data load: PASS
- Reference integrity: PASS
- Formula engine: PASS
- Schema adapters: PASS
- Snapshot/equipment: PASS
- EXP/leveling: PASS
- Regeneration: PASS
- Movement: PASS
- Remaining modules: UNKNOWN by design

## Next Recommended Milestone

Runtime-0007 — Spawn Runtime Foundation.
