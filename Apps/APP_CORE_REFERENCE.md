# Application Core Reference Policy

All Reforged applications depend on `Core/` for shared contracts, configuration, logging, validation, serialization, repository access, events, versioning, manifest loading, health checks, and utilities.

Applications covered by this policy:

- Server
- Importer
- Editor
- Launcher
- Client

Application folders must not define duplicate versions of Core models such as game records, entity identity, validation results, repository contracts, manifest state, logging events, or configuration providers.

Compile-level project references will be added as the application projects become real build targets. Until then, each application README must state that Core is its dependency source and that app-local duplicate models are forbidden.
