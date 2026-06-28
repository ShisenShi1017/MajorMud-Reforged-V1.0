# Core Library

The Core library is the shared foundation for MajorMud Reforged.

All applications must use Core for shared behavior instead of creating duplicate models or utility logic.

## Responsibilities

- Project and database version metadata
- Configuration models
- Logging contracts
- Serialization contracts
- Validation result models
- Game data models
- Import source provenance
- Repository health reporting
- Future event bus and service interfaces

## Current State

Code Update 0004 establishes the Core source layout and initial reusable contracts. It does not require the user to install the .NET SDK and does not claim that compiled executables exist yet.

## Rule

Server, Editor, Importer, Launcher, and Client should depend on Core concepts instead of defining independent versions of the same game data.
