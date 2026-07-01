# RuntimePrep0004 — Shop Inventory & Item Availability Extraction

Status: COMPLETE

Runtime used: no
Worldgroup used: no
Runtime observations used: 0

## Purpose
Extract an expected shop inventory and item-availability model for later runtime comparison.

## Evidence Inputs
- RuntimePrep0003 cumulative checkpoint
- DataDecode0110 static certification checkpoint
- MajorMud-Reforged uploaded source/package
- WCC shop/item codex JSON evidence
- WCCSHOPS decoded records and crosscheck evidence

## Metrics
- Shops loaded: 178
- Shops with non-empty inventory: 94
- Inventory edges: 1236
- Unique items available through shops: 853
- Items loaded from codex item evidence: 1950
- WCC shop binary records inspected: 145
- WCC shop name crosscheck hits: 140
- Inventory item reference issues: 0

## Outputs
- ShopCatalog_RuntimePrep0004.csv
- ShopInventoryMatrix_RuntimePrep0004.csv
- ItemShopAvailabilityMatrix_RuntimePrep0004.csv
- WCCShopBinaryCorroboration_RuntimePrep0004.csv
- WCCShopNameCrosscheck_RuntimePrep0004.csv
- ShopRuntimeChecklist_RuntimePrep0004.csv
- RuntimePrep0004_IssueRegister.csv
- RuntimePrep0004_Metrics.csv
- RuntimePrep0004_DecisionRegister.csv
- InputArtifactVerification_RuntimePrep0004.csv

## Certification Boundary
This pass is runtime preparation only. It does not prove live shop behavior, restocking, player access, pricing, transaction logic, or script-triggered inventory changes. Those remain pending RuntimeValidation.
