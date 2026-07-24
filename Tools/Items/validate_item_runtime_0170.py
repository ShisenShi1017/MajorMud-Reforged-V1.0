from pathlib import Path
import json
import sys

root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
source = (root / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke = (root / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")
required = [
    "Deposit(string instanceId, string ownerId, string bankId, int quantity)",
    "Withdraw(string instanceId, string ownerId, string bankId, int quantity)",
    "SellToMerchant(string instanceId, string ownerId, string merchantId, int quantity)",
    "BuyFromMerchant(string instanceId, string merchantId, string buyerId, int quantity)",
    "TransferQuantityCore(RuntimeItemInstance source, int quantity"
]
missing = [token for token in required if token not in source]
if missing:
    raise SystemExit("missing runtime references: " + ", ".join(missing))
for token in ["partial bank deposit failed", "partial bank withdrawal failed", "partial merchant sale failed", "partial merchant purchase failed"]:
    if token not in smoke:
        raise SystemExit("missing smoke coverage: " + token)
for rel in [
    "Evidence/Items/CodeUpdate0170/item_partial_bank_merchant_stack_semantics.json",
    "Librarium/Policies/runtime_item_partial_bank_merchant_stack_policy.json"
]:
    json.loads((root / rel).read_text(encoding="utf-8"))
print("CodeUpdate0170 item partial bank/merchant validation passed")
