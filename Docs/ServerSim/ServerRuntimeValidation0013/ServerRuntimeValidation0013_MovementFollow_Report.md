# Server Runtime Validation 0013 — Movement Follow Runtime Evidence

## Result
PARTIAL PASS for conservative canonical movement evidence scope.

## Scope
This pass continues after Server Runtime Validation 0012 assist/pack evidence. It targets the remaining Validation 0010 gap: movement-based follow into adjacent rooms.

## Runtime source of truth boundary
Canonical `/Data` remains the only runtime data source of truth. Runtime logs, imported databases, RuntimePrep, MMExplorer, MMBBSEmu, and archived databases remain evidence only and were not allowed to overwrite canonical data.

## Work executed
- Loaded the current repository working copy.
- Ran simulator `summary`.
- Ran simulator smoke tests with pytest.
- Parsed all uploaded runtime log text files for party-follow movement events using the pattern `-- Following your Party leader <direction> --`.
- Compared each parsed movement event against canonical `/Data/rooms.json` exits using a conservative room-name transition matcher.

## Results
- Simulator summary: completed.
- Smoke tests: 3 passed.
- Runtime follow movement events parsed: 140.
- PASS: 5.
- REVIEW: 135.
- FAIL: 0.

## Interpretation
The pass verifies that the simulator/repository can validate some runtime party-follow movements against canonical room exits without modifying `/Data`. The large REVIEW count is expected because many runtime rooms share repeated names such as `Dark Cave`, making room-name-only matching insufficient for safe promotion. Ambiguous or special movement rows were retained as evidence and not promoted.

## Data promotion
No canonical `/Data` file was changed. No new canonical data was promoted.

## Remaining gaps
- Resolve repeated-name room ambiguity using stronger route context, room numbers, map IDs, or command path traces.
- Validate special exits and hidden/scripted movement separately.
- Use imported database references as evidence to disambiguate repeated cave/mansion/sewer room transitions before promoting any canonical data changes.
