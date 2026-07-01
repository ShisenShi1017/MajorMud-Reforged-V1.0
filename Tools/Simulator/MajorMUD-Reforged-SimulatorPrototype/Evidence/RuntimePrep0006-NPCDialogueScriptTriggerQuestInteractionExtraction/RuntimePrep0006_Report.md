# RuntimePrep0006 — NPC Dialogue, Script Trigger & Quest Interaction Extraction

Status: COMPLETE (static runtime-preparation only)

Worldgroup used: no
Runtime observations used: 0

## Scope
This pass extracted candidate NPC/dialogue, prompt/script-trigger, shop interaction, and quest/progression text evidence from verified static sources. It did not certify live trigger behavior.

## Key Metrics
- WBB message files parsed: 4
- WBB message blocks extracted: 423
- WBB message definitions extracted: 414
- Dialogue/user-message candidates: 78
- Quest/progression candidates: 125
- Shop/economy interaction candidates: 35
- Script/prompt candidates: 150
- Runtime checklist rows: 245
- Repository quest records loaded: 0
- Unresolved issues preserved: 2

## Evidence Boundary
WBB/WCC message resources are secondary binary/static evidence. MDB/CSV and prior DataDecode certification remain authoritative for relational data. No static conclusion was modified.

## Preserved Uncertainties
- Exact live NPC ownership and trigger firing order still require Worldgroup or equivalent runtime behavior.
- Repository Data/quests.json was empty in this pass; quest candidates were therefore preserved from WBB/message text rather than inferred as canonical quest records.
- Parsed WBB message text confirms text/control resources, not live script execution.

## Next Step
RuntimePrep0007 — Combat/System Behavior Model.
