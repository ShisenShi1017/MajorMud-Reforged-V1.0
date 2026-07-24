from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in [
    "IReadOnlyList<string>? DisplacedEquipmentInstanceIds",
    "bool displaceEquipmentConflicts",
    "DisplaceEquipmentConflictsForRestorationCore",
    "displacedEquipment.AddRange",
    "foreach (var conflict in conflicts) Unequip(conflict.InstanceId, ownerId)"
]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in [
    "corpse-resurrection-equipment-conflict-displacement",
    "default resurrection restoration displaced occupied equipment",
    "authorized conflict displacement did not restore original equipment",
    "no-remove equipment was displaced during resurrection",
    "blocked displacement did not roll back corpse item"
]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in [
    "Evidence/Items/CodeUpdate0180/item_resurrection_equipment_conflict_displacement_semantics.json",
    "Evidence/Items/CodeUpdate0180/provenance.json",
    "Librarium/Policies/runtime_item_resurrection_equipment_conflict_displacement_policy.json"
]:
    json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0180 validation passed; canonical_items={len(items)}")
