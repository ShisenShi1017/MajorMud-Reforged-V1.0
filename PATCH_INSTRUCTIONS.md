# Patch Instructions - CodeUpdate0137

This is a local Codex repository update, not a ChatGPT ZIP patch.

## Verify Locally

From the repository root:

```powershell
python Tools\Validation\run_complete_runtime_verification.py
```

Expected result:

```text
PASS 76/76
Reports\RuntimeVerification\complete_runtime_verification_20260711T143632Z.md
```

## Commit/Push Gate

1. Clean smoke artifacts from `Saves/`.
2. Commit CodeUpdate0137.
3. Push the commit.
4. Wait for green GitHub Actions before starting the next public update.

## Rules

- Do not hand-edit generated `/Data` as canonical source.
- Keep Librarium authoritative.
- Preserve `ImportedDatabases/`.
- Do not commit smoke saves under `Saves/Runtime0137`.
- Do not claim GitHub Actions green until the current pushed commit is verified.
