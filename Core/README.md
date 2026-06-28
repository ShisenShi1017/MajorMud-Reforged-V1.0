# Core

`Core/` is the shared foundation for MajorMud Reforged.

Everything that must be consistent between the server, editor, importer, launcher, and future client belongs here.

## Responsibilities

- Reforged data models
- Database manifest handling
- JSON serialization rules
- Validation engine
- Import framework contracts
- Runtime world model contracts
- Logging contracts
- Event system contracts
- Configuration models
- Shared utilities

## Rule

Do not duplicate game models separately in Server, Editor, Importer, Launcher, or Client.

Those applications should depend on the same Core definitions so the entire project speaks the same data language.
