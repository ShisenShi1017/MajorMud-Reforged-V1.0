# CodeUpdate-0007 - Core Contracts Foundation

This update continues Milestone 1 by adding server-ready Core contracts without introducing a runtime dependency on the .NET SDK.

## Added contracts

- Event contracts and publisher/subscriber interfaces.
- Service registry contracts.
- Validation pipeline contracts and implementation shell.
- Editable data repository interfaces.
- Provenance tracking contracts.
- Runtime state model.
- Server-ready world model interface.

## Runtime rule

MajorMud Reforged must run from compiled executables or runtime-ready artifacts in `Bin/`. The normal runtime path must never depend on the .NET SDK, `SDK launch command`, or `SDK build command`.

## Update execution rule

This update is packaged to run from its own extracted folder. Run `Apply-CodeUpdate-0007-Core-Contracts-Foundation.bat` from the extracted update folder and point it at the main project folder if automatic detection cannot find it.
