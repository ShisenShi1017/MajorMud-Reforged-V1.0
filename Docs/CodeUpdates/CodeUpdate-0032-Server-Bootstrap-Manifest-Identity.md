# CodeUpdate-0032 - Server Bootstrap Manifest Identity

## Purpose

Improve non-gameplay Server bootstrap diagnostics by carrying ProjectManifest identity values into the bootstrap report and console output.

## Changes

- Added ProjectManifest identity fields to `ServerBootstrapReport`.
- Server bootstrap now captures project, CodeUpdate, database version, and milestone from `ProjectManifest.json`.
- Server bootstrap console output now prints those identity values.
- Smoke tests verify the identity fields are captured.
- Server README and continuity documents were updated.

## Handoff Suggestion Added

Future Code Updates should include a short next-scope candidate and explicit boundaries separately from completed work. This helps the next session continue smoothly without treating suggestions as already implemented.

## Boundaries

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No editor implementation.
- No networking expansion.

## Verification

Local .NET 8.0.422 restore/build/smoke verification passed. GitHub Actions remains the official validation gate after user push.
