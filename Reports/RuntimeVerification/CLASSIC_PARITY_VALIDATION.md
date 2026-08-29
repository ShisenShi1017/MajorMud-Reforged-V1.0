# Classic Parity Validation

The final focused static closure completed **530/530 checks** before consolidation. The cumulative ledger retains **98 PASS / 102 STRONG / 0 UNKNOWN** assertions.

The historical pass-specific validators and smoke command aliases are retired. `Tools/validator.py` is the single current-health entrypoint, and `--smoke-classic-parity-closure` is the retained runtime parity gate. Current build and smoke results are recorded in the repository manifests and continuity documents.

## Current local verification

- Reforged.Server Release build with warnings as errors: **PASS**, 0 warnings / 0 errors.
- Reforged.App Release build with warnings as errors: **PASS**, 0 warnings / 0 errors.
- Compiler.Tests Release build and executable regression: **PASS**, 104,911 records across 47 families.
- Clean Classic generation: **PASS**, 103,330 objects across 36 families; 26,668/26,668 room descriptions.
- Classic parity closure smoke: **PASS**, 19/19 retained checks.
- Core/server smoke: **PASS**, 17/17 checks.
- Live combat round: Nahr exact coverage and empty-argument rendering **PASS**; the full smoke remains **FAIL** only on the pre-existing `npc-blacknight-self-status-persisted` check.
- Python current-health validator: **NOT RUN**; `python` is absent and `py -0p` reports no installed interpreter.
- GitHub Actions: **PENDING** until push.
