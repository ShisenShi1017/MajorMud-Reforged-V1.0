# Codex Repo Update Tasks

## 1. Add current evidence docs
Create or update a documentation folder, suggested path:

`docs/recovery/`

Add:

- `CURRENT_STATE.md`
- `BEHAVIOR_STATUS.md`
- `RUNTIME_REQUIRED_REGISTER.md`
- `SIMULATOR_TESTING_POLICY.md`
- `EVIDENCE_CHAIN.md`

Use the text from this handoff as the starting point.

## 2. Preserve Build0024/0025 as expected-model test data
Do not present Build0024/0025 matrices as original runtime proof. Store them as expected-model fixtures, suggested path:

`tests/fixtures/expected_model/build0024/`
`tests/fixtures/expected_model/build0025/`

## 3. Create real executable simulator test harness
Suggested path:

`tests/simulator/`

Minimum test groups:

- load/static data smoke test
- world/room/shop/spawn query tests
- archetype creation tests
- inventory buy/sell projection tests if hooks exist
- combat attack projection tests if hooks exist
- spell/heal/sneak/economy/XP tests marked skipped/xfail until hooks exist

Important: skipped tests should explain the missing hook and whether the behavior is `runtime_required` or merely `not_implemented`.

## 4. Add formula-hook inventory
Create:

`docs/recovery/FORMULA_HOOK_INVENTORY.md`

For each subsystem list:

- executable hook exists? yes/no
- source/data support level
- current test coverage
- current confidence label
- remaining blocker

## 5. Add evidence labels to simulator output
Where simulator tests or CLI commands emit results, add a field such as:

- `evidence_class=simulator_verified`
- `evidence_class=expected_model`
- `evidence_class=verified_from_data`
- `evidence_class=runtime_required`

This prevents future confusion between model projections and verified original behavior.
