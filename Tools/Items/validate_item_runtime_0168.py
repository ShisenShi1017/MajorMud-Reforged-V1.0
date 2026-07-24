from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CS = ROOT / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs"
SMOKE = ROOT / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs"
REQUIRED = {
    "RuntimeItemDestructionResult": CS,
    "Destroy(string instanceId, string ownerId)": CS,
    "AdministrativeRemove": CS,
    "EnforceOwnershipTransferRestrictions": CS,
    "EnforceDestructionRestrictions": CS,
    "BOUND_OWNER_MISMATCH": CS,
    "QUEST_OWNER_MISMATCH": CS,
    "binding-quest-trade-and-destruction-integrity": SMOKE,
}
for token, path in REQUIRED.items():
    if token not in path.read_text(encoding="utf-8"):
        raise SystemExit(f"missing {token} in {path}")
for path in [
    ROOT / "Evidence/Items/CodeUpdate0168/item_binding_quest_destruction_semantics.json",
    ROOT / "Librarium/Policies/runtime_item_binding_quest_destruction_policy.json",
]:
    json.loads(path.read_text(encoding="utf-8"))
print("CodeUpdate0168 item runtime validation passed")
