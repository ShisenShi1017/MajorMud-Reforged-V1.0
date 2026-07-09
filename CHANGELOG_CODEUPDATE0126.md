# CodeUpdate 0126 — Librarium Compiler v1

## Summary
CodeUpdate 0126 adds the first Librarium compiler implementation and generates the first Classic `/Data` package from the Librarium object catalogs.

## Added
- `.NET` compiler CLI source at `Apps/Compiler`.
- `.NET` smoke-test project at `Apps/Compiler.Tests`.
- Reference Python compiler at `Tools/Librarium/compile_librarium_reference.py` for environments where the .NET SDK is unavailable.
- Generated Classic runtime data at `Data/Classic`.
- Compile manifest at `Data/Classic/compile_manifest.json`.
- Deterministic compile hash at `Data/Classic/compile_hash.json`.
- Compiler validation report at `Validation/Compiler/compiler_validation_0126.json`.

## Results
- Object families compiled: 32
- Objects compiled: 11,188
- Generated Classic data files: 35 including manifest/hash/client projection
- Deterministic output check: PASS
- Runtime gameplay code modified: No
- ImportedDatabases retained: Yes

## Important Environment Note
The execution container used for this update does not have the `dotnet` SDK installed, so the `.NET` compiler project was created but not built in-container. The reference Python compiler executed successfully and mirrors the v1 compiler contract for validation.

## Commands
Preferred .NET command once Codex/GitHub has .NET installed:

```bash
dotnet run --project Apps/Compiler -- build --world Classic --clean --root .
dotnet run --project Apps/Compiler.Tests --root .
```

Fallback validation command used in this environment:

```bash
python Tools/Librarium/compile_librarium_reference.py --world Classic --clean --root .
```
