# Core Testing

Core Update 0009 introduces a lightweight, dependency-free test foundation for Milestone 1.

Rules:

- Tests may exist as source-level development checks.
- Runtime must never require the .NET SDK.
- No test dependency may be required for launching compiled runtime artifacts from `Bin/`.
- Future formal test runners are allowed for development/build environments only.
