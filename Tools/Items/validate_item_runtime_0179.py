from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in [
    "death_original_equipped_slot",
    "bool restoreEquipment",
    "Equipment restoration requires an authoritative character record.",
    "reconciled = Equip(reconciled.InstanceId, characterId, character!, originalSlot)"
]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in [
    "corpse-resurrection-explicit-equipment-restoration",
    "death equipment slot was not recorded",
    "default resurrection restoration did not remain carried and unequipped",
    "explicit resurrection equipment restoration failed",
    "failed equipment restoration did not roll back"
]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in [
    "Evidence/Items/CodeUpdate0179/item_resurrection_equipment_restoration_semantics.json",
    "Evidence/Items/CodeUpdate0179/provenance.json",
    "Librarium/Policies/runtime_item_resurrection_equipment_restoration_policy.json"
]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0179 validation passed; canonical_items={len(items)}")
