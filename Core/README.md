# Core

Core is the shared foundation for MajorMud Reforged. Server, Editor, Importer, Launcher, and future Client code should all use Core models and services instead of creating duplicate logic.

This update adds SDK-free source foundations only. It does **not** require the .NET SDK and does **not** claim any executable exists.

## Current Core areas

- `Configuration/` - project path and runtime configuration models.
- `Logging/` - structured log event contracts and simple file logger source.
- `Validation/` - validation result and issue model used by database/server checks.
- `Versioning/` - project/database/code update version metadata.
- `Serialization/` - JSON helper contract for future app use.
- `Health/` - repository health model and report contracts.
- `Models/` - base game object contracts shared by server/editor/importer.
