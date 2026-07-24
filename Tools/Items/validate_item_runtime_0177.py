from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RuntimeCorpseRestorationResult","RestoreCorpseItems","Only the corpse character","CorpseFinalized"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["corpse-resurrection-item-restoration","resurrection did not restore item ownership","empty resurrection corpse was not finalized"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0177/item_resurrection_corpse_restoration_semantics.json","Librarium/Policies/runtime_item_resurrection_corpse_restoration_policy.json"]: json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0177 validation passed; canonical_items={len(items)}")
