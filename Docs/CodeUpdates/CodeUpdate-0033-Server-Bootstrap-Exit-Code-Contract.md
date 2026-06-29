# CodeUpdate-0033 - Server Bootstrap Exit Code Contract

## Purpose

Make non-gameplay Server bootstrap success and failure codes an explicit report contract for future host/server automation.

## Changes

- Added `ExitCode` to `ServerBootstrapReport`.
- Server startup now returns `report.ExitCode` for both success and failure.
- Smoke tests verify successful bootstrap exposes exit code 0.
- Smoke tests verify failed bootstrap exposes exit code 1.
- Continuity documents record CodeUpdate-0032 as green by user report.

## Boundaries

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No editor implementation.
- No networking expansion.
- No data schema changes.

## Verification

Local .NET 8.0.422 restore/build/smoke verification passed. GitHub Actions remains the official validation gate after user push.

## Next-Scope Candidate

After CodeUpdate-0033 is pushed and green, CodeUpdate-0034 should be another small host/server improvement approved by the user before implementation.
