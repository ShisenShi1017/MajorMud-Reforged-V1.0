# Classic Parity Validation

The cumulative ledger now retains **126 PASS / 74 STRONG / 0 UNKNOWN** assertions. Native verifier results for character death, death-floor placement, session lifecycle, party coordination, gang communications, modal invitations, daily cleanup, and organization services are retained in one generic consolidated report.

The historical pass-specific validators and smoke command aliases are retired. `Tools/validator.py` is the single current-health entrypoint, and `--smoke-classic-parity-closure` is the retained runtime parity gate. Current build and smoke results are recorded in the repository manifests and continuity documents.

## Current verification record

- Retained 0338 package native death-floor fingerprint/callsite/source result: **PASS 88/88**, 0 failures.

- Retained 0337 package native character death/lifecycle fingerprint/callsite/source result: **PASS 60/60**, 0 failures.
- Retained 0336 package native scheduler/regeneration fingerprint/callsite/source result: **PASS 69/69**, 0 failures.
- Retained 0335 package native NPC combat/RNG result: **PASS 49/49**, 0 failures.
- Retained 0334 package native DLL formula/callsite result: **PASS 66/66**, 0 failures.
- Assertion ledger/scorecard reconciliation: **PASS**, 126 PASS / 74 STRONG / 0 UNKNOWN.
- Consolidated retained native verifier results: **PASS**, 920 checks across 11 subsystems, 0 failures.
- Reforged.Server, Reforged.App, Reforged.Client, and core/server test Release builds with warnings as errors: **PASS**, 0 warnings / 0 errors.
- Clean Classic generation: **PASS**, 103,330 objects across 36 families and 26,668/26,668 room descriptions.
- Bundle D trap audit: **PASS**, 295/295 traps with 293 active and 2 hidden native states.
- Combat completion, combat runtime, full live-combat round, clock scheduling, and Classic parity closure smokes: **PASS**, 0 failures.
- Numbered one-off native validators were not restored. Their supplied results are retained in `native_runtime_verification.json` and are not presented as a new local execution.
- Broader item smoke: **FAIL**, two pre-existing corpse-resurrection equipment assertions remain open; all newly affected focused gates pass.
- GitHub Actions: **PENDING** until push.
