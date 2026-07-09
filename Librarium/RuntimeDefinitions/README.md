# Librarium Runtime Definitions

CodeUpdate 0116 makes the Librarium the explicit source of truth for runtime generation.

- Human-readable knowledge remains in `Librarium/Documentation` and `Librarium/MajorMUDKnowledgeBase`.
- Machine-readable definitions live here under `Librarium/RuntimeDefinitions`.
- `/Data` is no longer authoritative and is not stored as a committed source folder in this cleaned repo.
- `/Data` is generated per world by `Tools/Librarium/export_librarium_to_data.py`.

Runtime rule:

```text
Librarium -> validated export -> /Data/<World> -> server runtime -> read-only client view
```

Classic definitions are locked. Custom and AI worlds use validated overrides.
