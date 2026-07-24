from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RuntimeNpcCorpseLootResult","ReconcileNpcCorpseLoot","npc_loot_key","npc_loot_target_quantity","NPC corpse loot reconciliation does not remove"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["npc-corpse-loot-target-and-identity","NPC corpse loot did not reach projected target","idempotent NPC corpse loot reconciliation changed identities"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0186/item_npc_corpse_loot_target_semantics.json","Evidence/Items/CodeUpdate0186/provenance.json","Librarium/Policies/runtime_item_npc_corpse_loot_target_policy.json"]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0186 validation passed; canonical_items={len(items)}")
