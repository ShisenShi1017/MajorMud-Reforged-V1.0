# RuntimePrep0003 — Navigation and Exit Extraction

## Status
Complete. This is a runtime-preparation pass only. No Worldgroup runtime observations were used.

## Purpose
Build an expected navigation and exit model for later live validation. The pass preserves DataDecode0101–0110 and RuntimePrep0001–0002, then adds a navigation-focused layer from available ImportedDatabases / MegaMudMP map-path evidence.

## Evidence Handling
- MDB and mdbtools CSV evidence remain the authoritative static relationship layer.
- WBB/WCC/MegaMudMP-style map artifacts are treated as secondary binary/path corroboration.
- Runtime behavior is not certified until observed in a running Worldgroup environment.

## Metrics
- Navigation source `.mp` files scanned: 1957
- Navigation/path edges extracted: 1182
- Unique room codes observed in path headers: 653
- Area transition paths: 165
- Intra-area paths: 1017
- Reverse-map paths present: 1066
- One-way candidates requiring runtime confirmation: 116
- Special/hidden/scripted navigation candidates: 232
- Runtime observations used: 0

## Produced Files
- NavigationEdgeMatrix_RuntimePrep0003.csv
- OneWayExitCandidateMatrix_RuntimePrep0003.csv
- SpecialNavigationCandidateMatrix_RuntimePrep0003.csv
- AreaTransitionMatrix_RuntimePrep0003.csv
- NavigationCommandSummary_RuntimePrep0003.csv
- NavigationRuntimeChecklist_RuntimePrep0003.csv
- RuntimePrep0003_Metrics.csv
- RuntimePrep0003_DecisionRegister.csv
- InputArtifactVerification_RuntimePrep0003.csv

## Uncertainties Preserved
- A path file proves an expected route/path relationship, not live engine behavior.
- One-way status is a candidate classification based on missing reverse map evidence, not runtime certainty.
- Hidden/search/scripted/teleport-like behaviors require Worldgroup confirmation.

## Next Pass
RuntimePrep0004 — Shop Inventory and Item Availability Extraction.
