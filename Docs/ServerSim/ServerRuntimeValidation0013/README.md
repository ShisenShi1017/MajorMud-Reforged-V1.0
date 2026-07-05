# Server Runtime Validation 0013

This folder preserves the user-supplied `ServerRuntimeValidation0013_MovementFollow_Handoff.zip` as a folded CodeUpdate-0064 input.

## Scope

Validation0013 is movement/follow runtime evidence only. It compares party-follow movement text against canonical `/Data/rooms.json` exits using conservative room-name transition matching.

## Result

- Result: PARTIAL PASS.
- Runtime follow movement events parsed: 140.
- PASS: 5.
- REVIEW: 135.
- FAIL: 0.
- Simulator smoke tests in the handoff: 3 passed.
- Canonical `/Data` files changed: false.
- Canonical data promotion: none.

## Boundary

Runtime logs, imported databases, RuntimePrep, MMExplorer, MMBBSEmu, archived databases, and simulator output remain evidence only. This validation does not promote canonical data, implement movement execution, implement party follow, start world simulation, start sockets/sessions, or certify live server parity.

## Next Evidence Need

Resolve repeated-room movement ambiguity with route context, source room IDs, imported database evidence, and/or simulator path traces before promoting any movement transition.
