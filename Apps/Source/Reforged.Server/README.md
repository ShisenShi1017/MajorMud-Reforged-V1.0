# Reforged.Server

Milestone 2 host/server startup shell for the Server application.

This project explicitly references `Core/Source/Reforged.Core/Reforged.Core.csproj` to satisfy the rule that every application depends on Core and Core depends on no application project.

## Current Status

- CodeUpdate-0029 added startup-only Server diagnostics.
- CodeUpdate-0031 adds a non-gameplay Server bootstrap entry point.
- CodeUpdate-0033 adds an explicit bootstrap report exit-code contract.
- CodeUpdate-0035 adds bootstrap validation issue counts for startup output and future host tooling.
- CodeUpdate-0038 adds required-directory status reporting to the bootstrap report and startup output.
- CodeUpdate-0039 adds a host-tool readiness summary while keeping gameplay status disabled.
- CodeUpdate-0041 adds a read-only Classic parity audit inventory while keeping gameplay disabled.

The bootstrap path verifies repository root discovery, required project folders, ProjectManifest identity, existing Server startup diagnostics, and read-only Classic parity inventory. It also captures the manifest project, CodeUpdate, database version, and milestone for startup output. It does not start gameplay systems.

CodeUpdate-0032 extends the bootstrap report so those manifest identity fields are available to future host/server diagnostics and handoff output. CodeUpdate-0033 adds a reusable `ExitCode` value for automation and startup shell handling. CodeUpdate-0035 adds issue counts by validation severity. CodeUpdate-0038 exposes required-directory statuses and counts for future host/editor tooling. CodeUpdate-0039 adds a compact readiness summary for host/editor tooling. CodeUpdate-0041 adds broad Classic parity inventory for canonical `Data/` and imported reference material.

## Boundaries

The Server project still does not implement:

- Gameplay systems.
- Combat.
- Character creation.
- World simulation loop.
- Networking expansion.
- Live editor behavior.
- Exact MajorMUD parity claims.
- Proprietary WCC/WBB/WSGSERVE binary parsing.

Runtime rule: source project builds are development artifacts. Normal runtime must use compiled/runtime-ready artifacts from `Bin/` and must not require the .NET SDK.
