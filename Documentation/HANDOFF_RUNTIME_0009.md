# Handoff — Runtime-0009 Combat

Use this ZIP as the current cumulative runtime baseline.

## Major correction

The first 0009 response was not a proper milestone package. This redo builds an actual Runtime-0009 package from Runtime-0008 and adds executable combat files, validator registration, documentation, manifest, changelog, and a fresh verification report.

## Added files

- `Runtime/combat/__init__.py`
- `Runtime/combat/combat_runtime.py`
- `Sim/validators/validate_combat.py`
- `Documentation/COMBAT_STRATEGY_0009.md`
- `Documentation/HANDOFF_RUNTIME_0009.md`
- `CHANGELOG_RUNTIME_0009.md`
- `MANIFEST_RUNTIME_0009.json`

## Modified files

- `Sim/verification_registry.py`
- `Sim/run_verification_suite.py`
- `Sim/validators/validate_runtime_placeholders.py`
- `Reports/verification_suite_report.json`

## Validation

Combat validator status: PASS.

Observed executable combat test:

- Duel produced rounds and events.
- Attack pipeline produced hit/miss events.
- Combat provenance remained `not_classic_claimed`.
- Direct attack produced traceable hit chance and damage application.

## Remaining unknowns

- Classic hit formula
- Classic damage formula
- Critical hits
- Dual wield
- Backstab
- Status effects in combat
- Spell combat
- Exact speed/timing behavior
