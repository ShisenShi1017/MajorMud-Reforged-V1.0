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
- CodeUpdate-0042 adds read-only canonical Server data catalog loading while keeping gameplay disabled.
- CodeUpdate-0043 adds runtime-readiness classification for canonical Server data categories while keeping gameplay disabled.
- CodeUpdate-0057 adds a read-only game module catalog while keeping gameplay disabled.
- CodeUpdate-0058 adds a read-only world projection audit and closes the `world_rooms` loader projection gap while keeping gameplay disabled.
- CodeUpdate-0059 adds read-only module readiness diagnostics while keeping gameplay and runtime rule profiles disabled.
- CodeUpdate-0060 adds read-only host configuration diagnostics while keeping gameplay, networking, and world simulation disabled.
- CodeUpdate-0061 adds read-only host lifecycle readiness diagnostics while keeping gameplay, networking, and world simulation disabled.
- CodeUpdate-0062 adds read-only scheduler bootstrap diagnostics while keeping scheduler execution, gameplay, networking, and world simulation disabled.

The bootstrap path verifies repository root discovery, required project folders, ProjectManifest identity, host configuration, canonical Server data catalog loading, world projection loading, game module catalog loading, module readiness, host lifecycle readiness, scheduler bootstrap diagnostics, existing Server startup diagnostics, and read-only Classic parity inventory. It also captures the manifest project, CodeUpdate, database version, and milestone for startup output. It does not start gameplay systems.

CodeUpdate-0032 extends the bootstrap report so those manifest identity fields are available to future host/server diagnostics and handoff output. CodeUpdate-0033 adds a reusable `ExitCode` value for automation and startup shell handling. CodeUpdate-0035 adds issue counts by validation severity. CodeUpdate-0038 exposes required-directory statuses and counts for future host/editor tooling. CodeUpdate-0039 adds a compact readiness summary for host/editor tooling. CodeUpdate-0041 adds broad Classic parity inventory for canonical `Data/` and imported reference material. CodeUpdate-0042 loads registered Core data categories and reports record readiness for Server startup. CodeUpdate-0043 separates runtime-ready categories from empty placeholder categories. CodeUpdate-0057 registers `majormud-classic` as the first disabled data-driven module baseline, proving MajorMUD is a module hosted by the Server rather than the whole Server implementation. CodeUpdate-0058 projects `Data/world.json` rooms against `Data/rooms.json` and reports the world room index before any world simulation exists. CodeUpdate-0059 reports `majormud-classic` as host-ready for non-gameplay planning while gameplay and runtime rule profiles remain disabled. CodeUpdate-0060 reports host configuration defaults while gameplay, networking, and world simulation remain disabled. CodeUpdate-0061 reports the host lifecycle as `ready-runtime-disabled` while gameplay, networking, and world simulation remain disabled. CodeUpdate-0062 reports scheduler bootstrap channels as `scheduler-bootstrap-registered-runtime-disabled` while scheduler execution, gameplay, networking, and world simulation remain disabled.

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
- Runtime use of imported/reference data.

`majormud-classic` is currently a module registration only. Its `classic` rule profile is intentionally disabled until a reviewed gameplay/runtime slice is approved.

Runtime rule: source project builds are development artifacts. Normal runtime must use compiled/runtime-ready artifacts from `Bin/` and must not require the .NET SDK.
