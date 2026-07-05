# Session Handoff — Server Runtime Validation 0013

## Last completed validation
Server Runtime Validation 0013 — Movement Follow Runtime Evidence.

## Result
PARTIAL PASS for conservative movement-follow evidence scope.

## Evidence generated
- Evidence/Validation0013/runtime_follow_movement_validation.csv
- Evidence/Validation0013/runtime_follow_movement_summary.json
- Evidence/Validation0013/command_outputs/simulator_summary_0013.txt
- Evidence/Validation0013/command_outputs/pytest_0013.txt
- Evidence/Validation0013/command_outputs/movement_validation_output_0013.txt
- Codex/Validation0013/verify_movement_follow.py

## Key counts
- Runtime follow movement events parsed: 140
- PASS: 5
- REVIEW: 135
- FAIL: 0

## Canonical data status
No `/Data` files changed or promoted.

## Next recommended pass
Resolve repeated-room movement ambiguity using route context, source room IDs, imported database evidence, and/or simulator path traces. Do not promote any transition unless canonical `/Data` can be verified safely.
