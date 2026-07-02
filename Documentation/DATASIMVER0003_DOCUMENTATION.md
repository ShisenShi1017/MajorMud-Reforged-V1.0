# DataSimVer0003 Summary — Standard Exit Validation

## Policy
- `/Data` was treated as read-only.
- No room endpoints were promoted.
- No special-exit semantics were promoted.
- Typed compass exits were documented only; their runtime behavior was not inferred.
- Evidence is expected-model simulator validation evidence, not original runtime proof.

## Results
- Room records: 1246
- Unique room IDs: 1246
- Standard compass-direction rows inspected: 3022
- Valid/resolved rows: 2811
- Missing destination rows: 210
- Ambiguous destination rows: 1
- Invalid destination rows: 0
- Blank destination rows: 0
- Plain standard compass rows: 2515
- Typed compass rows documented: 507
- Resolved direct edges: 2811
- Resolved direct edges present in `room_graph.json`: 2694
- Resolved direct edges absent from `room_graph.json`: 117
- Reciprocal direct edges: 2684
- Non-reciprocal direct edges: 127
- Duplicate from-room/direction rows: 0
- Room endpoints promoted: 0
- Special-exit semantics promoted: 0

## Output Files
- `/Validation/DataSimVer0003/StandardExitValidation.csv`
- `/Validation/DataSimVer0003/StandardExitInvalidOrMissing.csv`
- `/Validation/DataSimVer0003/StandardExitTypedCompassRows.csv`
- `/Validation/DataSimVer0003/StandardExitReciprocity.csv`
- `/Validation/DataSimVer0003/StandardExitOneWay.csv`
- `/Validation/DataSimVer0003/StandardExitDuplicateDirections.csv`
- `/Validation/DataSimVer0003/StandardExitMapStats.csv`
- `/Validation/DataSimVer0003/Coverage.json`
- `/Evidence/DataSimVer0003/DataSimVer0003_evidence_summary.json`

## Conclusion
DataSimVer0003 completed as a bounded standard-exit validation pass. It documents direct compass exits, missing or ambiguous destinations, room-graph support, and reciprocal link status without changing `/Data`.
