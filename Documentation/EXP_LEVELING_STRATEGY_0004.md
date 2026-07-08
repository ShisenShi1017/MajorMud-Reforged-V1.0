# Runtime-0004 EXP + Leveling Calculator Strategy

## Scope
Runtime-0004 adds a deterministic EXP and leveling calculator foundation.

## What is implemented
- `Runtime/leveling.py`
- Reforged EXP required curve: `1000 * (level - 1)^2`, modified by class/race EXP modifiers when present in `/Data`
- Level-from-EXP calculation
- CP award calculation using `/Data/leveling.json`
- Snapshot application helper
- `Sim/validators/validate_exp_leveling.py`

## Evidence boundary
This milestone does **not** claim exact classic MajorMUD EXP parity.

The calculator is tagged as:

```text
mode: reforged_formula
classic_accuracy: not_classic_claimed
```

Classic EXP formulas should only be promoted later if proven from MMExplorer, MBBSEmu, native data, or repeatable runtime evidence.

## Design reason
This gives the runtime a working progression module now while preserving the rule that unverified classic formulas are not promoted as classic behavior.
