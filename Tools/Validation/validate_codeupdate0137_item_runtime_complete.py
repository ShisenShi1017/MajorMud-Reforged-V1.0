from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parents[2]
code = (root / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke = (root / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")
program = (root / "Apps/Source/Reforged.Server/Program.cs").read_text(encoding="utf-8")
required = [
    "RuntimeItemLocations",
    "RuntimeItemInstance",
    "RuntimeItemEngine",
    "RuntimeItemValidator",
    "RuntimeItemWorldRepository",
    "GetAll(",
    "Drop(",
    "Give(",
    "Put(",
    "GetFromContainer(",
    "Equip(",
    "Wear(",
    "Unequip(",
    "MoveToNpc(",
    "MoveToTrade(",
    "CreateCorpse(",
    "Loot(",
    "CleanupExpiredCorpses(",
    "Atomic<",
    "Duplicate item instance ID",
    "TWO_HANDED_EXCLUDES_OFF_HAND_AND_SHIELD",
]
missing = [token for token in required if token not in code]
base = root / "Data" / "Classic"
for name in ["items.json", "equipment.json", "equipment_rules.json", "equipment_slots.json"]:
    path = base / name
    if not path.exists():
        missing.append(str(path.relative_to(root)))
    else:
        data = json.loads(path.read_text(encoding="utf-8"))
        if data.get("authority") != "Librarium":
            missing.append(f"{path}: authority")
for token in [
    "create-ground",
    "get-all",
    "container-put-get",
    "corpse-create-transfer",
    "duplicate-prevention-and-rollback",
    "save-load-world-state",
    "disconnect-reconnect-snapshot",
]:
    if token not in smoke:
        missing.append("smoke:" + token)
if "--smoke-items" not in program:
    missing.append("Program --smoke-items")
print(json.dumps({"passed": not missing, "missing": missing}, indent=2))
sys.exit(0 if not missing else 1)
