from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RestoredGold","RestoreCorpseForResurrection","restoreGold ? corpse.Gold : 0","Only the corpse character can receive resurrection restoration"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["corpse-resurrection-explicit-gold-restoration","item-only restoration mutated corpse gold","explicit resurrection gold restoration failed","non-owner resurrection restoration was accepted"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0178/item_resurrection_gold_restoration_semantics.json","Evidence/Items/CodeUpdate0178/provenance.json","Librarium/Policies/runtime_item_resurrection_gold_restoration_policy.json"]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0178 validation passed; canonical_items={len(items)}")
