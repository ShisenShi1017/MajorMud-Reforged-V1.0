# Codex Handoff — CodeUpdate 0126

## Current baseline
CodeUpdate 0126 — Librarium Compiler v1.

## Critical rules
1. Librarium is the authoritative source of truth.
2. `/Data` is generated runtime output and must not be hand-edited.
3. `ImportedDatabases` must remain until the Librarium is declared 100% complete by the project owner.
4. Runtime gameplay code is unchanged in this update.
5. Client is read-only and world-scoped.
6. Host/editor edits Librarium, then validates/compiles to `/Data`.

## Compiler
Primary implementation:

```bash
dotnet run --project Apps/Compiler -- build --world Classic --clean --root .
```

Smoke test:

```bash
dotnet run --project Apps/Compiler.Tests --root .
```

Fallback/reference compiler used in this ChatGPT container:

```bash
python Tools/Librarium/compile_librarium_reference.py --world Classic --clean --root .
```

## Validation note
The ChatGPT execution container did not have the .NET SDK installed. The .NET compiler source is present but was not built here. The reference compiler generated `Data/Classic` and passed deterministic output validation.

## Next task
Install/confirm .NET 8 SDK, build `Apps/Compiler`, run `Apps/Compiler.Tests`, compare generated output with `Data/Classic/compile_hash.json`, then begin server load tests against generated `Data/Classic`.
