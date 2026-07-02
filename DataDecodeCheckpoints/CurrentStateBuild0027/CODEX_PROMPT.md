You are continuing MajorMUD Reforged recovery. Update the repository to preserve the current state through Build0027.

Critical rules:
- Never fabricate evidence, metrics, pass counts, ZIPs, logs, or verification.
- Verify every incoming artifact before using it.
- Verify every outgoing artifact before reporting it.
- Preserve the cumulative evidence chain.
- Mark unresolved behaviors as runtime_required.
- Do not claim original Worldgroup behavior unless supported by decoded evidence or observed runtime.
- Treat Build0024/0025 matrices as expected_model unless converted into real executable simulator tests.
- Treat prior chat pass/fail claims as non-authoritative unless backed by actual logs in the artifacts.

Primary repo work:
1. Add recovery documentation from this handoff under docs/recovery/.
2. Add/organize expected-model fixtures from Build0024 and Build0025.
3. Add real executable simulator test scaffolding.
4. Add skipped/xfail tests for missing formula hooks.
5. Add a formula-hook inventory and runtime-required register.
6. Run tests that actually exist and commit generated logs/reports only if produced by the run.

Next build should be Build0028 Real Executable Harness, not another inferred matrix.
