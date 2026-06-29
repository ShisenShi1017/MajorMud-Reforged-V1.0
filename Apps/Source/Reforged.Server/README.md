# Reforged.Server

Milestone 2 host/server startup shell for the Server application.

This project explicitly references `Core/Source/Reforged.Core/Reforged.Core.csproj` to satisfy the Milestone 1 rule that every application depends on Core and Core depends on no application project.

CodeUpdate-0029 adds startup-only Server diagnostics. The diagnostics path wires the Server shell through existing Core configuration, logging, service registry, event bus, repository health, and startup diagnostics without starting gameplay systems.

Runtime rule: this source project is not a user runtime path. Normal runtime must use compiled/runtime-ready artifacts from `Bin/` and must not require the .NET SDK.
