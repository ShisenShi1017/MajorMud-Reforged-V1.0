# Core Architecture

Core is the shared library layer for Reforged. Every application should use Core for shared contracts.

## Code Update 0005 Additions

The following source foundations were added:

- `Core/Configuration/ProjectPaths.cs`
- `Core/Configuration/RuntimeConfiguration.cs`
- `Core/Logging/LogLevel.cs`
- `Core/Logging/LogEvent.cs`
- `Core/Logging/IReforgedLogger.cs`
- `Core/Logging/FileReforgedLogger.cs`
- `Core/Validation/ValidationSeverity.cs`
- `Core/Validation/ValidationIssue.cs`
- `Core/Validation/ValidationResult.cs`
- `Core/Validation/IValidator.cs`
- `Core/Versioning/ProjectVersionInfo.cs`
- `Core/Serialization/JsonDocumentEnvelope.cs`
- `Core/Health/RepositoryHealthIssue.cs`
- `Core/Health/RepositoryHealthReport.cs`
- `Core/Models/GameEntity.cs`

## Design Rule

Applications must not duplicate models or validation logic. Server, Editor, Importer, Launcher, and Client should all consume the Core contracts.
