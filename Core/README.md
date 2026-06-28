# MajorMud Reforged Core

Core is the shared foundation for every MajorMud Reforged application.

Core owns cross-application contracts and helpers only. Server, Importer, Editor, Launcher, and Client may reference Core, but Core must never reference those applications.

## Milestone 1 responsibilities

Core provides:

- configuration contracts and provider helpers
- logging contracts and dependency-free file logging
- validation pipeline contracts and implementations
- serialization contracts and JSON serializer
- repository interfaces
- shared data models
- event system contracts and in-memory event bus
- dependency injection/service registry contracts and implementation
- version management helpers
- project manifest models and reader
- repository health checker
- shared utility library

## Runtime rule

Normal runtime must never require the .NET SDK. Distributed applications launch from compiled/runtime-ready artifacts in `Bin/`. SDK-based commands may exist only for development/build environments and must not be the routine user launch path.
