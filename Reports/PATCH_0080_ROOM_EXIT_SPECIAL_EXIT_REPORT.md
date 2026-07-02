# Patch 0080 — Room-Exit Endpoint Evidence Recovery and Special-Exit Semantics

## Objective
Recover room-exit endpoint evidence and classify special-exit semantics using the current cumulative repository data. This patch does not guess endpoint records and does not promote behavior semantics without direct evidence.

## Inputs Used
- Patch 0079 cumulative package extracted from `/mnt/data/MajorMUD-Reforged-Patch0079-TargetedBlockerResolution-Cumulative.zip`
- Recreated `Data/rooms.json`
- Recreated `Data/room_graph.json`
- Patch 0079 remaining room-exit and parity issue reports

## Runtime Data Policy
- Recreated `/Data` included: Yes
- Core `/Data` modified: No
- Old ZIPs included: No
- Synthetic room records created: No
- Special-exit behaviors promoted: 0

## Results
- Room-exit endpoint rows reviewed: 211
- Room-exit endpoints resolved by direct evidence: 1
- Room-exit endpoints still unresolved: 210
- Special-exit candidate rows reviewed: 48
- Distinct special `exit_type` values reviewed: 13
- Runtime parity issues reviewed: 367
- Runtime parity issues remaining after 0080: 366

## Resolved Evidence
Patch 0080 resolves the `wcc_map12_room1 -> wcc_map12_room3158` endpoint as **Verified with documented variance** because `Data/room_graph.json` contains a direct graph edge for that route. No synthetic `Data/rooms.json` room record was created.

## Still Blocked
All other remaining room-exit endpoints remain blocked because the expected endpoint ID was not found in either `Data/rooms.json` or `Data/room_graph.json` coordinates/edges.

Special-exit behavior semantics remain blocked. Patch 0080 records candidate rows and `exit_type` counts only. No behavior implementation is recommended until direct decoder or live runtime evidence proves the behavior.

## Required Reports
- `Reports/room_exit_endpoint_recovery_0080.csv`
- `Reports/room_exit_endpoint_resolved_0080.csv`
- `Reports/room_exit_endpoint_remaining_0080.csv`
- `Reports/special_exit_candidate_rows_0080.csv`
- `Reports/special_exit_type_semantics_0080.csv`
- `Reports/runtime_parity_issues_0080.csv`
- `Reports/runtime_parity_issues_remaining_0080.csv`
- `Reports/recreated_data_checksums_0080.csv`
- `Reports/source_evidence_inventory_0080.csv`

## Confidence
- Resolved graph endpoint: High
- Remaining blocked endpoints: High that they are unresolved in current `/Data` and graph evidence
- Special-exit behavior semantics: Low/Medium evidence only; blocked from promotion
