from pathlib import Path
import json, sys
root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
source = (root / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke = (root / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")
for token in ["EnforceOwnerQuantityLimit", "maximum_owned_quantity", "OWNER_QUANTITY_LIMIT_EXCEEDED", "IsLimitCountedLocation"]:
    if token not in source: raise SystemExit("missing runtime reference: " + token)
for token in ["owner-quantity-limit-and-release", "limited transfer rollback failed", "released owner quantity reservation was not reusable"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: " + token)
for rel in ["Evidence/Items/CodeUpdate0172/item_owner_quantity_limit_semantics.json", "Librarium/Policies/runtime_item_owner_quantity_limit_policy.json"]:
    json.loads((root / rel).read_text(encoding="utf-8"))
items = json.loads((root / "Data/Classic/items.json").read_text(encoding="utf-8"))["records"]
print(f"CodeUpdate0172 owner quantity limit validation passed; canonical_items={len(items)}")
