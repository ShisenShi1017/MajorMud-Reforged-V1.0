# CodeUpdate-0033 - Server Bootstrap Exit Code Contract Report

## Summary

CodeUpdate-0033 makes Server bootstrap exit-code behavior explicit on the bootstrap report so future host/server automation can consume the same success/failure contract that `Program.cs` returns.

## Implementation

- `ServerBootstrapReport` now exposes `ExitCode`.
- Server startup returns `report.ExitCode`.
- Smoke tests verify success returns 0 and failure returns 1.
- CodeUpdate-0032 is recorded as green by user report.

## Boundaries

- No gameplay features were added.
- No combat, character creation, world simulation, editor implementation, or networking expansion was added.
- No BAT installer or update ZIP was created by Codex.
- Codex did not push to GitHub.

## Verification

Local .NET 8.0.422 restore/build/smoke verification passed. An initial test build caught a misplaced assertion against the Core startup diagnostics report; the assertion was moved into the Server bootstrap smoke test and the rerun passed.

## Next-Scope Candidate

CodeUpdate-0034 should remain a small approved host/server slice.
