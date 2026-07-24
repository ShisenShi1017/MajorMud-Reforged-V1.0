from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RuntimeQuestItemGrantResult","RuntimeQuestItemConsumptionResult","GrantQuestItem","ConsumeQuestItem","quest_grant_key","quest_grant_target_quantity"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["quest-item-grant-consume-and-rollback","quest grant did not reach target quantity","failed quest consume changed inventory"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0187/item_quest_grant_consumption_semantics.json","Evidence/Items/CodeUpdate0187/provenance.json","Librarium/Policies/runtime_item_quest_grant_consumption_policy.json"]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0187 validation passed; canonical_items={len(items)}")
