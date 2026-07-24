from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CS = ROOT / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs"
SMOKE = ROOT / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs"
REQUIRED = {
    "RequireCarried": CS,
    "EnforceTransferRestrictions": CS,
    "Broken item cannot be equipped": CS,
    "no_remove": CS,
    "INVALID_RESTRICTION_METADATA": CS,
    "equipment-and-transfer-restriction-integrity": SMOKE,
}
for token, path in REQUIRED.items():
    text = path.read_text(encoding="utf-8")
    if token not in text:
        raise SystemExit(f"missing {token} in {path}")
for path in [
    ROOT / "Evidence/Items/CodeUpdate0167/item_equipment_transfer_restriction_semantics.json",
    ROOT / "Librarium/Policies/runtime_item_equipment_transfer_restriction_policy.json",
]:
    json.loads(path.read_text(encoding="utf-8"))
print("CodeUpdate0167 item runtime validation passed")
