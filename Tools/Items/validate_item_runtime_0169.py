from __future__ import annotations
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
CS = ROOT / "Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs"
SMOKE = ROOT / "Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs"
REQUIRED = {
    'public const string Bank = "bank"': CS,
    'public const string Merchant = "merchant"': CS,
    'Deposit(string instanceId, string ownerId, string bankId)': CS,
    'Withdraw(string instanceId, string ownerId, string bankId)': CS,
    'SellToMerchant': CS,
    'BuyFromMerchant': CS,
    '"bank" => "no_bank"': CS,
    '"sell" => "no_sell"': CS,
    'bank-and-merchant-transfer-integrity': SMOKE,
}
for token, path in REQUIRED.items():
    if token not in path.read_text(encoding="utf-8"):
        raise SystemExit(f"missing {token} in {path}")
for path in [
    ROOT / "Evidence/Items/CodeUpdate0169/item_bank_merchant_transfer_semantics.json",
    ROOT / "Librarium/Policies/runtime_item_bank_merchant_transfer_policy.json",
]:
    json.loads(path.read_text(encoding="utf-8"))
print("CodeUpdate0169 item runtime validation passed")
