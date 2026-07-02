# Recommended Next Pass: Build0028 Real Executable Harness

Objective: replace expected-model-only matrices with actual executable simulator tests wherever hooks exist.

## Steps

1. Unpack the current repo ZIP.
2. Locate simulator entry points and CLI/test framework.
3. Run existing tests without modification; capture logs.
4. Add smoke tests that only use currently executable hooks.
5. Add skipped/xfail tests for missing formula hooks.
6. Convert Build0024 archetype rows into executable tests where the simulator supports character creation/action APIs.
7. Convert Build0025 formula rows into executable sensitivity tests only after the corresponding formula hook exists.
8. Produce a report with:
   - PASS
   - FAIL
   - SKIPPED_MISSING_HOOK
   - RUNTIME_REQUIRED
   - EXPECTED_MODEL_ONLY

## Required caution

Do not fabricate pass counts. Counts must come from test logs generated during the Codex run.
