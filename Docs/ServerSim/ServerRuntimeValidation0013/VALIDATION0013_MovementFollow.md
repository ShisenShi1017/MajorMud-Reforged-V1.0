# Validation 0013 — Movement Follow Checklist

## Checks

| Check | Result | Evidence |
|---|---:|---|
| Simulator summary runs | PASS | Evidence/Validation0013/command_outputs/simulator_summary_0013.txt |
| Smoke tests run | PASS | Evidence/Validation0013/command_outputs/pytest_0013.txt |
| Runtime follow movements parsed | PASS | Evidence/Validation0013/runtime_follow_movement_validation.csv |
| Canonical exit comparison performed | PASS | Evidence/Validation0013/runtime_follow_movement_validation.csv |
| Canonical data promoted | NOT PROMOTED | No `/Data` files included |

## Confidence
Medium for the 5 PASS rows because they match canonical room-name transitions directly.
Low for REVIEW rows because repeated runtime room names and special exits require stronger disambiguation before any canonical promotion.
