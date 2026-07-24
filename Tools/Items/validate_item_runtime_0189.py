from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RuntimeEventItemGrantResult","RuntimeEventItemCleanupResult","GrantEventItem","ReconcileEventItemCleanup","event_cleanup_protected","event_reward_key"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["event-item-reward-cleanup-target-and-rollback","event reward did not reconcile exact target quantity","failed event cleanup changed protected item state"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0189/item_event_reward_cleanup_semantics.json","Evidence/Items/CodeUpdate0189/provenance.json","Librarium/Policies/runtime_item_event_lifecycle_policy.json"]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0189 validation passed; canonical_items={len(items)}")
