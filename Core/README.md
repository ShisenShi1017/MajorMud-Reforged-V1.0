# Reforged Core

Core is the shared foundation for the active Reforged applications.

The compiled project is `Core/Source/Reforged.Core/Reforged.Core.csproj`; shared
application contracts live in `Core/Applications`. Core owns cross-application contracts
and helpers only and must never reference an application project.

## Responsibilities

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

## Runtime Rule

Normal runtime must never require the .NET SDK. Distributed applications launch from
published artifacts. SDK-based commands exist only for development, compilation, and
validation.
