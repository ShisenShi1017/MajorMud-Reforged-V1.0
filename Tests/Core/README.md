# Core Tests

This folder is reserved for Core-only tests and development checks.

Milestone 1 policy:

- Core tests must not depend on Server, Importer, Editor, Launcher, or Client projects.
- Tests must not introduce a normal-runtime dependency on the .NET SDK.
- Tests validate shared contracts, repository health, serialization, validation, event dispatch, and manifest reading.
