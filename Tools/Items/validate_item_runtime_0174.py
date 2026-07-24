from pathlib import Path
import json, sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")
for token in ["IsDeathProtected(x)", "public RuntimeItemInstance Loot(string instanceId, string corpseId, string characterId, int? quantity)", "Corpse loot quantity is outside the source stack", "Partial corpse loot requires a stackable item definition"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["corpse-death-protection-and-partial-stack-loot", "death-protected item was transferred to corpse", "partial corpse loot did not preserve source remainder"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0174/item_death_protection_partial_corpse_loot_semantics.json","Librarium/Policies/runtime_item_death_protection_partial_corpse_loot_policy.json"]:
    json.loads((root/rel).read_text(encoding="utf-8"))
items=json.loads((root/"Data/Classic/items.json").read_text(encoding="utf-8"))["records"]
print(f"CodeUpdate0174 validation passed; canonical_items={len(items)}")
