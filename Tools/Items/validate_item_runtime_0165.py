from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
required = [
    ROOT / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs",
    ROOT / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs",
    ROOT / "Evidence/Items/CodeUpdate0165/item_charge_consumption_semantics.json",
    ROOT / "Librarium/Policies/runtime_item_charge_and_consumption_policy.json",
    ROOT / "Reports/DataDecode/items_runtime_semantic_exhaustion_0165.md",
]
for path in required:
    if not path.is_file():
        raise SystemExit(f"missing required file: {path.relative_to(ROOT)}")
for path in required[2:4]:
    json.loads(path.read_text(encoding="utf-8"))
source = required[0].read_text(encoding="utf-8")
for symbol in (
    "RuntimeItemConsumptionResult",
    "SpendCharges",
    "Recharge",
    "ConsumeQuantity",
    "MaximumCharges",
    "INVALID_CHARGES",
    "INVALID_DURABILITY",
    "CHARGES_EXCEED_MAXIMUM",
):
    if symbol not in source:
        raise SystemExit(f"missing runtime symbol: {symbol}")
smoke = required[1].read_text(encoding="utf-8")
if "charges-consumption-and-recharge-rollback" not in smoke:
    raise SystemExit("missing charge/consumption smoke coverage")
items_path = ROOT / "Data/Classic/items.json"
if items_path.is_file():
    items = json.loads(items_path.read_text(encoding="utf-8"))
    assert items.get("authority") == "Librarium"
    records = items.get("records", [])
    assert all(record.get("id", "").startswith("item.") for record in records)
    print(f"PASS item-runtime-0165 records={len(records)} consumable={sum(bool(x.get('consumable')) for x in records)} charged={sum(int(x.get('default_charges') or 0) > 0 for x in records)}")
else:
    print("PASS item-runtime-0165 overlay structural validation; Data/Classic/items.json validated in merged repository gate")
