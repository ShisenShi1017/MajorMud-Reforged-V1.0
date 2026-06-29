# CodeUpdate-0028 - Milestone 2 Scope Plan

## Purpose

Record the user-reported green CodeUpdate-0027 GitHub Actions result and define the first Milestone 2 scope plan.

## Current State

- Milestone 1 - Core Framework is frozen.
- CodeUpdate-0027 GitHub Actions was reported green by the user.
- Milestone 2 is open for planning.

## Recommended First Implementation

CodeUpdate-0029 should start with host/server startup and runtime diagnostics foundation.

Recommended scope:

- Wire Server startup through existing Core configuration.
- Use the existing Core logging abstractions.
- Use the existing service registry/DI foundation.
- Run repository health checks at startup.
- Run Core startup diagnostics from the Server path.
- Produce clear startup diagnostics output for future troubleshooting.

Boundaries:

- No gameplay systems.
- No combat.
- No character creation.
- No world simulation loop.
- No editor implementation.
- No networking expansion unless explicitly approved.

## Verification

CodeUpdate-0028 is planning-only. Local .NET 8.0.422 restore/build/smoke-test verification passed for Core, Server, Importer, Editor, Launcher, Client, and the Core smoke-test project.

GitHub Actions remains the official validation gate after push.
