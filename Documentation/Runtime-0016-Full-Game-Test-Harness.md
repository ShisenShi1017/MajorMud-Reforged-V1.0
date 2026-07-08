# Runtime-0016 — Full Game Test Harness

Runtime-0016 adds a broad automated harness that runs the offline game runtime through the major systems built so far. It is intended to catch integration breakage before the runtime is merged into the existing server/host work.

## What this milestone tests

- Canonical data loading
- Offline world boot
- Class/race profile construction
- Ability flag grant/query
- Quest surface readiness
- Command parser and dispatcher
- Movement command execution
- Inventory/equipment surfaces
- Combat command path
- Spell command path and status tick hooks
- Loot generation
- Banking/economy probe
- NPC healer service probe
- Tick scheduler, AI, spawn, regeneration, cleanup events
- Persistence save creation
- Scripted playthrough
- Formula provenance/unknown policy

## Classic accuracy policy

This harness does not convert UNKNOWN mechanics into verified classic behavior. UNKNOWN is expected where exact classic formulas, quest records, hidden passives, or timing rules still require MMExplorer/MBBSEmu/runtime evidence.

## Main entry points

```bash
python Sim/run_full_game_harness.py
python Sim/run_verification_suite.py
```

## Reports

- `Reports/full_game_test_harness_0016.json`
- `Reports/full_game_test_harness_validation_0016.json`
- `Reports/verification_suite_report.json`

## Result from build verification

- Full game harness: PASS_WITH_UNKNOWN
- Passed checks: 34
- Failed checks: 0
- Unknown checks: 7
- Full validation suite: PASS / UNKNOWN by design
