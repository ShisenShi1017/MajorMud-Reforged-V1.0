# Reforged.Client

Milestone 1 source shell for the Client application.

This project explicitly references `Core/Source/Reforged.Core/Reforged.Core.csproj` to satisfy the Milestone 1 rule that every application depends on Core and Core depends on no application project.

Runtime rule: this source project is not a user runtime path. Normal runtime must use compiled/runtime-ready artifacts from `Bin/` and must not require the .NET SDK.
