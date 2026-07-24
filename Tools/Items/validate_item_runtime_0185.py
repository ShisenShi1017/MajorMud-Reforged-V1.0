from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RuntimeNpcCorpseTransferResult","CreateNpcCorpse","npc_no_drop","no_loot","CountSubtree","NPC identity is required for corpse creation"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["npc-death-corpse-transfer-and-retention","NPC drop was not transferred to corpse ownership","NPC no-drop item was not retained"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0185/item_npc_death_corpse_transfer_semantics.json","Evidence/Items/CodeUpdate0185/provenance.json","Librarium/Policies/runtime_item_npc_death_corpse_transfer_policy.json"]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0185 validation passed; canonical_items={len(items)}")
