# Reforged.Server

Milestone 2 host/server startup shell for the Server application.

This project explicitly references `Core/Source/Reforged.Core/Reforged.Core.csproj` to satisfy the rule that every application depends on Core and Core depends on no application project.

## Current Status

- CodeUpdate-0029 added startup-only Server diagnostics.
- CodeUpdate-0031 adds a non-gameplay Server bootstrap entry point.

The bootstrap path verifies repository root discovery, required project folders, ProjectManifest identity, and existing Server startup diagnostics. It does not start gameplay systems.

## Boundaries

The Server project still does not implement:

- Gameplay systems.
- Combat.
- Character creation.
- World simulation loop.
- Networking expansion.
- Live editor behavior.

Runtime rule: source project builds are development artifacts. Normal runtime must use compiled/runtime-ready artifacts from `Bin/` and must not require the .NET SDK.
