from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text()
for token in ["RuntimeRoomItemResetResult","ResetRoomItem","room_reset_key","room_reset_target_quantity","Room item reset does not remove instances above the projected target"]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in ["room-item-reset-target-and-identity","idempotent room item reset created duplicate instances","failed room item reset rollback changed room inventory"]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in ["Evidence/Items/CodeUpdate0182/item_room_reset_identity_semantics.json","Evidence/Items/CodeUpdate0182/provenance.json","Librarium/Policies/runtime_item_room_reset_identity_policy.json"]: json.loads((root/rel).read_text())
items=json.loads((root/"Data/Classic/items.json").read_text())["records"]
print(f"CodeUpdate0182 validation passed; canonical_items={len(items)}")
