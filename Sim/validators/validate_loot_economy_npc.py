from __future__ import annotations
from pathlib import Path
from dataclasses import asdict, is_dataclass
from Runtime.adapters import load_schema_bundle
from Runtime.loot.loot_runtime import LootRuntime
from Runtime.economy.economy_runtime import EconomyRuntime, Wallet
from Runtime.npc_services.npc_services_runtime import NPCServiceRuntime


def validate_loot_economy_npc(repo_root: Path):
    passed = failed = unknown = 0
    notes = []
    bundle = load_schema_bundle(repo_root / "Data")
    def _records(value):
        if isinstance(value, dict):
            seq = list(value.values())
        else:
            seq = list(value or [])
        out = []
        for row in seq:
            if is_dataclass(row):
                out.append(asdict(row))
            elif isinstance(row, dict):
                out.append(row)
        return out

    items = _records(getattr(bundle, "items", []))
    monsters = _records(getattr(bundle, "monsters", []))
    shops = _records(getattr(bundle, "shops", []))
    loot_tables = _records(getattr(bundle, "loot_tables", []))

    if items and monsters:
        passed += 1
        notes.append(f"Loaded runtime inputs: items={len(items)}, monsters={len(monsters)}, shops={len(shops)}, loot_tables={len(loot_tables)}")
    else:
        failed += 1
        notes.append("Missing required items or monsters for Runtime-0010 validation.")

    loot = LootRuntime(items=items, loot_tables=loot_tables, seed=10)
    monster = monsters[0] if monsters else {"id": "test_monster"}
    loot_result = loot.generate_monster_loot(monster, room_id="validation_room")
    if loot_result.corpse and any(e.event_type == "corpse_created" for e in loot_result.events):
        passed += 1
        notes.append(f"Loot/corpse pipeline PASS for source={loot_result.source_id}; events={len(loot_result.events)}")
    else:
        failed += 1
        notes.append("Loot/corpse pipeline did not produce a corpse event.")

    economy = EconomyRuntime(items=items, shops=shops)
    sample_item = next((str(i.get('runtime_id') or i.get('id') or i.get('item_id') or i.get('ItemID') or '') for i in items if isinstance(i, dict)), "")
    buy = economy.buy_price(sample_item)
    sell = economy.sell_price(sample_item)
    if buy.success and sell.success and buy.amount >= 0 and sell.amount >= 0:
        passed += 1
        notes.append(f"Economy price pipeline PASS for item={sample_item}; buy={buy.amount}; sell={sell.amount}")
    else:
        failed += 1
        notes.append("Economy pricing pipeline failed.")

    wallet = Wallet(gold=100, bank_gold=25)
    dep = economy.deposit(wallet, 40)
    wd = economy.withdraw(wallet, 10)
    if dep.success and wd.success and wallet.gold == 70 and wallet.bank_gold == 55:
        passed += 1
        notes.append("Bank deposit/withdraw state transition PASS.")
    else:
        failed += 1
        notes.append(f"Bank state transition FAIL: wallet={wallet}")

    npc = NPCServiceRuntime()
    heal = npc.healer_quote(missing_hp=30)
    inn = npc.inn_quote(level=5)
    train = npc.trainer_quote(level=5, skill_rank=2)
    travel = npc.travel_quote(destination_room_id="room_1", base_cost=15)
    if all(e.success and e.cost >= 0 for e in [heal, inn, train, travel]):
        passed += 1
        notes.append("NPC service quote pipeline PASS for healer/inn/trainer/travel.")
    else:
        failed += 1
        notes.append("NPC service quote pipeline failed.")

    unknown += 5
    notes.append("Classic loot probabilities, exact shop charm math, trainer requirements, healer behavior, and inn rest rules remain UNKNOWN until evidence-backed.")
    return {"status": "PASS" if failed == 0 else "FAIL", "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
