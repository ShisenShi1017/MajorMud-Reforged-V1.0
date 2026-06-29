# Reforged.Server

Milestone 2 host/server startup shell for the Server application.

This project explicitly references `Core/Source/Reforged.Core/Reforged.Core.csproj` to satisfy the rule that every application depends on Core and Core depends on no application project.

## Current State

CodeUpdate-0029 added startup-only Server diagnostics. The diagnostics path wires the Server shell through existing Core configuration, logging, service registry, event bus, repository health, and startup diagnostics without starting gameplay systems.

CodeUpdate-0030 updates this README to keep app documentation synchronized with the current GitHub truth.

## Runtime Rule

This source project is not the user runtime path. Normal runtime must use compiled/runtime-ready artifacts from `Bin/` and must not require the .NET SDK.

Future executable location:

`Bin/Windows/Server/`

## Boundaries

Do not add these without explicit approval:

- Gameplay systems
- Combat
- Character creation
- World simulation loop
- Networking expansion
- Live editor implementation
