# Reforged.Server

Milestone 2 host/server startup shell for the Server application.

This project explicitly references `Core/Source/Reforged.Core/Reforged.Core.csproj` to satisfy the rule that every application depends on Core and Core depends on no application project.

## Current Status

- CodeUpdate-0029 added startup-only Server diagnostics.
- CodeUpdate-0031 adds a non-gameplay Server bootstrap entry point.
- CodeUpdate-0033 adds an explicit bootstrap report exit-code contract.
- CodeUpdate-0035 adds bootstrap validation issue counts for startup output and future host tooling.

The bootstrap path verifies repository root discovery, required project folders, ProjectManifest identity, and existing Server startup diagnostics. It also captures the manifest project, CodeUpdate, database version, and milestone for startup output. It does not start gameplay systems.

CodeUpdate-0032 extends the bootstrap report so those manifest identity fields are available to future host/server diagnostics and handoff output. CodeUpdate-0033 adds a reusable `ExitCode` value for automation and startup shell handling. CodeUpdate-0035 adds issue counts by validation severity.

## Boundaries

The Server project still does not implement:

- Gameplay systems.
- Combat.
- Character creation.
- World simulation loop.
- Networking expansion.
- Live editor behavior.

Runtime rule: source project builds are development artifacts. Normal runtime must use compiled/runtime-ready artifacts from `Bin/` and must not require the .NET SDK.
