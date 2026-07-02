# DataSimVer0002 Summary — World Graph Traversal

## Policy
- /Data was treated as read-only.
- No room endpoints were promoted.
- No special-exit semantics were promoted.
- Simulator evidence is labeled as expected-model evidence, not original runtime proof.

## Results
- Start room: `wcc_map1_room1`
- Room records: 1246
- Unique room IDs: 1246
- Room graph edges: 10097
- Valid graph edges: 2694
- Invalid graph edges: 2
- Reachable rooms from start: 334
- Unreachable rooms from start: 912
- Dead-end rooms: 13
- One-way links: 99
- Room exit rows inspected: 3022
- Room exit rows resolved for evidence: 2811
- Ambiguous room-number endpoints: 1
- Missing room-number endpoints: 210

## Output Files
- `/Validation/DataSimVer0002/TraversalTrace.csv`
- `/Validation/DataSimVer0002/ExitResolutionTrace.csv`
- `/Validation/DataSimVer0002/InvalidDestinations.csv`
- `/Validation/DataSimVer0002/UnreachableRooms.csv`
- `/Validation/DataSimVer0002/DeadEnds.csv`
- `/Validation/DataSimVer0002/OneWayLinks.csv`
- `/Validation/DataSimVer0002/Coverage.json`
- `/Evidence/DataSimVer0002/DataSimVer0002_evidence_summary.json`

## Conclusion
DataSimVer0002 completed as a bounded traversal pass. It does not reduce or promote the remaining static endpoint gaps; it creates auditable traversal coverage evidence for later targeted passes.
