# Patch Instructions

This is a local Codex repository update, not a standalone ZIP patch.

To verify the closed CodeUpdate0138 baseline locally:

```text
python Tools\Validation\run_complete_runtime_verification.py
```

Expected result:

```text
PASS 55/55
```

Latest verified report:

```text
Reports/RuntimeVerification/complete_runtime_verification_20260712T173531Z.md
```

After commit and push, treat the update as accepted only when GitHub Actions is green for the pushed commit.
