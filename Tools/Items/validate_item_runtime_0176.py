from pathlib import Path
import json, sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")
for token in [
    "string? LimitedItemReservationOwnerId = null",
    "ResolveLimitOwnerCore",
    "corpse.LimitedItemReservationOwnerId",
    "active corpse reservations",
    "including active corpse reservations"
]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in [
    "corpse-limited-item-reservation-and-release",
    "corpse-held limited item did not reserve owner quantity",
    "corpse decay did not release limited-item reservation"
]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in [
    "Evidence/Items/CodeUpdate0176/item_corpse_limited_reservation_semantics.json",
    "Librarium/Policies/runtime_item_corpse_limited_reservation_policy.json"
]:
    json.loads((root/rel).read_text(encoding="utf-8"))
items=json.loads((root/"Data/Classic/items.json").read_text(encoding="utf-8"))["records"]
print(f"CodeUpdate0176 validation passed; canonical_items={len(items)}")
