from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs"
SMOKE = ROOT / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs"
EVIDENCE = ROOT / "Evidence/Items/CodeUpdate0166/item_durability_break_repair_semantics.json"
POLICY = ROOT / "Librarium/Policies/runtime_item_durability_break_repair_policy.json"
ITEMS = ROOT / "Data/Classic/items.json"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(message)


source = SOURCE.read_text(encoding="utf-8")
smoke = SMOKE.read_text(encoding="utf-8")
evidence = json.loads(EVIDENCE.read_text(encoding="utf-8"))
policy = json.loads(POLICY.read_text(encoding="utf-8"))
items = json.loads(ITEMS.read_text(encoding="utf-8"))["records"]

for token in (
    "RuntimeItemDurabilityResult",
    "DamageDurability(",
    "RepairDurability(",
    "MaximumDurability(",
    "DURABILITY_EXCEEDS_MAXIMUM",
    "BROKEN_STATE_MISMATCH",
):
    require(token in source, f"missing runtime durability token: {token}")

require("durability-damage-break-and-repair" in smoke, "missing durability smoke coverage")
require(evidence["code_update"] == "0166", "incorrect evidence update")
require(policy["code_update"] == "0166", "incorrect policy update")
require(len(items) == 1950, f"unexpected Classic item count: {len(items)}")
require(all(str(record.get("id", "")).startswith("item.") for record in items), "noncanonical item id")
require(sum(1 for record in items if int(record.get("durability", 0) or 0) > 0) == 0, "Classic durability projection changed; review evidence")
require(sum(1 for record in items if int(record.get("maximum_durability", 0) or 0) > 0) == 0, "Classic maximum durability projection changed; review evidence")
print("PASS CodeUpdate0166 item durability validation")
