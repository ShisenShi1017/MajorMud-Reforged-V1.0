from pathlib import Path
import json
import sys

root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
source = (root / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke = (root / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")
for token in [
    "MergeIntoDestinationCore(RuntimeItemInstance transferred)",
    "OrderBy(candidate => candidate.InstanceId, StringComparer.OrdinalIgnoreCase)",
    "INVALID_MAXIMUM_STACK",
    "STACK_EXCEEDS_MAXIMUM"
]:
    if token not in source:
        raise SystemExit("missing runtime reference: " + token)
for token in [
    "destination stack merge failed",
    "destination stack overflow identity failed",
    "full destination stack absorption failed"
]:
    if token not in smoke:
        raise SystemExit("missing smoke coverage: " + token)
for rel in [
    "Evidence/Items/CodeUpdate0171/item_destination_stack_reconciliation_semantics.json",
    "Librarium/Policies/runtime_item_destination_stack_reconciliation_policy.json"
]:
    json.loads((root / rel).read_text(encoding="utf-8"))
items = json.loads((root / "Data/Classic/items.json").read_text(encoding="utf-8"))["records"]
stackable = sum(1 for item in items if item.get("stackable") is True)
print(f"CodeUpdate0171 destination stack reconciliation validation passed; classic_stackable={stackable}")
