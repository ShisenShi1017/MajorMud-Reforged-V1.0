from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
required = [
    ROOT / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs",
    ROOT / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs",
    ROOT / "Evidence/Items/CodeUpdate0164/item_stack_instance_semantics.json",
    ROOT / "Librarium/Policies/runtime_item_instance_and_stack_policy.json",
]
for path in required:
    if not path.is_file():
        raise SystemExit(f"missing required file: {path.relative_to(ROOT)}")
for path in required[2:]:
    json.loads(path.read_text(encoding="utf-8"))
source = required[0].read_text(encoding="utf-8")
for symbol in ("SplitStack", "MergeStacks", "TransferQuantity", "CIRCULAR_CONTAINER", "CONTAINER_OWNER_MISMATCH"):
    if symbol not in source:
        raise SystemExit(f"missing runtime symbol: {symbol}")
items = json.loads((ROOT / "Data/Classic/items.json").read_text(encoding="utf-8"))
assert items.get("authority") == "Librarium"
assert all(record.get("id", "").startswith("item.") for record in items.get("records", []))
print(f"PASS item-runtime-0164 records={len(items.get('records', []))} stackable={sum(bool(x.get('stackable')) for x in items.get('records', []))}")
