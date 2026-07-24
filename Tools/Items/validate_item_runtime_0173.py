from pathlib import Path
import json, sys

root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
source = (root / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke = (root / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")

for token in [
    "SetContainedDescendantOwnerCore",
    "CollectSubtreeIdsCore",
    "var roots = _items.Values",
    "Circular container hierarchy encountered during recursive item cleanup",
    "SetContainedDescendantOwnerCore(transferred.InstanceId, characterId)"
]:
    if token not in source:
        raise SystemExit("missing runtime reference: " + token)

for token in [
    "corpse-container-hierarchy-preservation",
    "corpse-decay-recursive-container-cleanup",
    "nested container hierarchy was flattened",
    "corpse decay orphaned nested contents"
]:
    if token not in smoke:
        raise SystemExit("missing smoke coverage: " + token)

for rel in [
    "Evidence/Items/CodeUpdate0173/item_corpse_container_hierarchy_semantics.json",
    "Librarium/Policies/runtime_item_corpse_container_hierarchy_policy.json"
]:
    json.loads((root / rel).read_text(encoding="utf-8"))

items = json.loads((root / "Data/Classic/items.json").read_text(encoding="utf-8"))["records"]
print(f"CodeUpdate0173 corpse container hierarchy validation passed; canonical_items={len(items)}")
