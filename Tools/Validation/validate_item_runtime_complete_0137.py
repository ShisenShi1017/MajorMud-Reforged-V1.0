from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parents[2]
errors = []

runtime_path = root / "Apps" / "Source" / "Reforged.Server" / "RuntimeItemsEquipment.cs"
smoke_path = root / "Apps" / "Source" / "Reforged.Server" / "RuntimeItemSmokeReport.cs"
program_path = root / "Apps" / "Source" / "Reforged.Server" / "Program.cs"

for path in (runtime_path, smoke_path, program_path):
    if not path.exists():
        errors.append(f"Missing required runtime file: {path.relative_to(root)}")

if not errors:
    runtime = runtime_path.read_text(encoding="utf-8")
    smoke = smoke_path.read_text(encoding="utf-8")
    program = program_path.read_text(encoding="utf-8")

    required_runtime_tokens = [
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
    for token in required_runtime_tokens:
        if token not in runtime:
            errors.append(f"Runtime item contract missing token: {token}")

    required_smokes = [
        "create-ground",
        "get",
        "get-all",
        "give",
        "equip",
        "unequip",
        "wear-remove",
        "container-put-get",
        "corpse-create-transfer",
        "corpse-loot",
        "duplicate-prevention-and-rollback",
        "save-load-world-state",
        "disconnect-reconnect-snapshot",
        "runtime-validator",
    ]
    for token in required_smokes:
        if token not in smoke:
            errors.append(f"Item smoke is missing check: {token}")

    if "--smoke-items" not in program:
        errors.append("Program.cs does not expose --smoke-items")

for name in ("items.json", "equipment.json", "equipment_rules.json", "equipment_slots.json"):
    path = root / "Data" / "Classic" / name
    if not path.exists():
        errors.append(f"Missing generated authority file: {path.relative_to(root)}")
        continue
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"Invalid generated JSON {path.relative_to(root)}: {exc}")
        continue
    if not isinstance(payload, dict) or payload.get("authority") != "Librarium":
        errors.append(f"Generated file is not Librarium-authoritative: {path.relative_to(root)}")

print(json.dumps({"passed": not errors, "errors": errors}, indent=2))
sys.exit(0 if not errors else 1)
