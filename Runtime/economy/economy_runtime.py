"""Runtime-0010 economy, shop, bank, and repair runtime foundation."""
from __future__ import annotations
from dataclasses import dataclass, field, asdict
from typing import Any, Dict, List, Optional

@dataclass
class Wallet:
    gold: int = 0
    bank_gold: int = 0

@dataclass
class EconomyEvent:
    event_type: str
    success: bool
    amount: int = 0
    item_id: Optional[str] = None
    notes: List[str] = field(default_factory=list)
    classic_accuracy: str = "not_classic_claimed"


def _first(row: Dict[str, Any], *keys: str, default: Any = None) -> Any:
    for k in keys:
        if k in row and row[k] not in (None, ""):
            return row[k]
    return default


def _as_int(value: Any, default: int = 0) -> int:
    try:
        return int(float(value))
    except Exception:
        return default


class EconomyRuntime:
    def __init__(self, items: List[Dict[str, Any]] | None = None, shops: List[Dict[str, Any]] | None = None):
        self.items = items or []
        self.shops = shops or []
        self.item_index = {str(_first(i, "runtime_id", "id", "item_id", "ItemID", "number", default=n)): i for n, i in enumerate(self.items)}

    def item_value(self, item_id: str) -> int:
        row = self.item_index.get(str(item_id), {})
        return max(0, _as_int(_first(row, "value", "Value", "cost", "Cost", "price", "Price", default=_first(row.get("economy", {}) if isinstance(row.get("economy", {}), dict) else {}, "value", "price", "cost", default=0))))

    def buy_price(self, item_id: str, markup_percent: int = 125, charm_modifier_percent: int = 100) -> EconomyEvent:
        base = self.item_value(item_id)
        price = (base * max(0, markup_percent) * max(0, charm_modifier_percent)) // 10000
        return EconomyEvent("buy_price", True, amount=price, item_id=str(item_id),
                            notes=["Pricing formula is Reforged foundation unless matched to classic evidence later."])

    def sell_price(self, item_id: str, buyback_percent: int = 50, charm_modifier_percent: int = 100) -> EconomyEvent:
        base = self.item_value(item_id)
        price = (base * max(0, buyback_percent) * max(0, charm_modifier_percent)) // 10000
        return EconomyEvent("sell_price", True, amount=price, item_id=str(item_id),
                            notes=["Buyback formula is Reforged foundation unless matched to classic evidence later."])

    def deposit(self, wallet: Wallet, amount: int) -> EconomyEvent:
        amount = max(0, int(amount))
        if wallet.gold < amount:
            return EconomyEvent("bank_deposit", False, amount=amount, notes=["Not enough carried gold."])
        wallet.gold -= amount; wallet.bank_gold += amount
        return EconomyEvent("bank_deposit", True, amount=amount)

    def withdraw(self, wallet: Wallet, amount: int) -> EconomyEvent:
        amount = max(0, int(amount))
        if wallet.bank_gold < amount:
            return EconomyEvent("bank_withdraw", False, amount=amount, notes=["Not enough banked gold."])
        wallet.bank_gold -= amount; wallet.gold += amount
        return EconomyEvent("bank_withdraw", True, amount=amount)

    def repair_price(self, item_id: str, durability_missing: int, repair_percent: int = 10) -> EconomyEvent:
        base = self.item_value(item_id)
        price = (base * max(0, durability_missing) * max(0, repair_percent)) // 10000
        return EconomyEvent("repair_price", True, amount=price, item_id=str(item_id), notes=["Repair pricing is placeholder/foundation, not classic claimed."])

    def to_dict(self, event: EconomyEvent | Wallet) -> Dict[str, Any]:
        return asdict(event)
