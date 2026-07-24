from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RuntimeContainerRestockResult","RestockPortableContainer","container_restock_key","container_restock_target_quantity","Container restock does not remove contents above the projected target"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["portable-container-restock-target-and-identity","idempotent container restock created duplicate contents","failed container restock rollback changed contents"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0183/item_portable_container_restock_semantics.json","Evidence/Items/CodeUpdate0183/provenance.json","Librarium/Policies/runtime_item_portable_container_restock_policy.json"]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0183 validation passed; canonical_items={len(items)}")
