# CodeUpdate0135 - Final Exhaustive Classic Fixes

## Summary

CodeUpdate0135 applies the final exhaustive Classic fix payload over the verified CodeUpdate0134 room/exit/session/NPC baseline.

Included scope:

- Generated Classic bug-fix policy data.
- DLL-verified single-swing Classic combat smoke support.
- Repaired Python validation and complete runtime verification tooling.
- Duplicate trigger canonicalization with evidence retained.
- `room.unknown` runtime exclusion.
- Final simulation and complete runtime verification reports.

## Verification

- `python Tools/Validation/validate_codeupdate0135_exhaustive_repairs.py`: PASS.
- `python Tools/Validation/run_complete_runtime_verification.py`: PASS, 49/49.
- `Reports/SimulationVerification/CodeUpdate0135-FinalSimulationReport.md`: PASS, 38/38.
- Complete runtime verification included .NET restore/build, compiler tests, Core/server smoke tests, and server generated-data, room-exit, NPC, combat, and scripted local-session smokes.

## Boundaries

- CodeUpdate0136 remains required for complete Classic single-player gameplay.
- Do not create partial public CodeUpdate0136 packages or commits.
- Do not edit `/Data` manually; it remains generated runtime output from the Librarium.
- Do not touch `ImportedDatabases/`.
- Do not guess missing Classic behavior.
- Unknown shop item rows must be classified before treating them as missing data.
