# CodeUpdate0136 Internal - Classic Drop Roll Repair

## Summary

This internal CodeUpdate0136 phase fixes Classic kill-time loot drop percentage resolution.

The reported bug was a rounded endpoint-bucket calculation. A rounded expression such as `(random()*99+1)::INT` can make endpoint buckets half width, causing 1% drops to land at roughly half the intended rate and 3% drops to under-drop as well.

## Implemented

- Added Librarium rule `loot_rule.classic.kill_drop_percent_roll_0136`.
- Runtime kill-loot drop checks now use a full-width integer `1..100` roll.
- Drop success is `roll <= drop_percent`.
- Added `Tools/Validation/validate_codeupdate0136_classic_drop_roll.py`.
- Added the new validator to complete runtime verification.

## Verification

- `python Tools/Validation/validate_codeupdate0136_classic_drop_roll.py`: PASS.
- `python Tools/Validation/run_complete_runtime_verification.py`: PASS, 50/50.
- Fresh report: `Reports/RuntimeVerification/complete_runtime_verification_20260710T223106Z.md`.

## Boundaries

- This is an internal CodeUpdate0136 phase, not the finished public CodeUpdate0136.
- No imported/reference data was changed.
- No manual generated `/Data` rewrite was performed for this rule.
- Complete Classic single-player gameplay remains in progress.
