"""Runtime-0010 loot/corpse runtime foundation.

This module is executable but conservative: it only uses explicit canonical fields
when present and labels inferred/reforged behavior as not classic claimed.
"""
from __future__ import annotations
from dataclasses import dataclass, field, asdict
from random import Random
from typing import Any, Dict, List, Optional

@dataclass
class LootEvent:
    event_type: str
    source_id: str
    item_id: Optional[str] = None
    quantity: int = 1
    gold: int = 0
    provenance: str = "runtime_0010_foundation_not_classic_claimed"
    notes: List[str] = field(default_factory=list)

@dataclass
class Corpse:
    corpse_id: str
    owner_id: str
    room_id: str
    items: List[str] = field(default_factory=list)
    gold: int = 0
    decay_ticks: int = 60
    provenance: str = "runtime_0010_foundation_not_classic_claimed"

@dataclass
class LootResult:
    source_id: str
    events: List[LootEvent]
    corpse: Optional[Corpse]
    classic_accuracy: str = "not_classic_claimed"
    formula_mode: str = "reforged_foundation"


def _first(row: Dict[str, Any], *keys: str, default: Any = None) -> Any:
    for k in keys:
        if k in row and row[k] not in (None, ""):
            return row[k]
    return default


def _as_int(value: Any, default: int = 0) -> int:
    try:
        if isinstance(value, bool):
            return int(value)
        return int(float(value))
    except Exception:
        return default


class LootRuntime:
    def __init__(self, items: List[Dict[str, Any]] | None = None, loot_tables: List[Dict[str, Any]] | None = None, seed: int = 1):
        self.items = items or []
        self.loot_tables = loot_tables or []
        self.random = Random(seed)
        self.item_index = {str(_first(i, "runtime_id", "id", "item_id", "ItemID", "number", default=len(self.items))): i for i in self.items}

    def gold_from_monster(self, monster: Dict[str, Any]) -> LootEvent:
        # Use explicit gold fields if the data exposes them; otherwise zero with provenance.
        min_gold = _as_int(_first(monster, "min_gold", "gold_min", "MinGold", "GoldMin", default=0))
        max_gold = _as_int(_first(monster, "max_gold", "gold_max", "MaxGold", "GoldMax", default=min_gold))
        if max_gold < min_gold:
            max_gold = min_gold
        gold = self.random.randint(min_gold, max_gold) if max_gold > min_gold else min_gold
        return LootEvent(event_type="gold", source_id=str(_first(monster, "runtime_id", "id", "monster_id", "name", default="unknown_monster")), gold=gold,
                         notes=["Gold uses explicit min/max fields when available; otherwise defaults to 0."])

    def item_drops_from_monster(self, monster: Dict[str, Any]) -> List[LootEvent]:
        source = str(_first(monster, "runtime_id", "id", "monster_id", "name", default="unknown_monster"))
        events: List[LootEvent] = []
        raw = _first(monster, "items", "drops", "loot", "Treasure", "treasure", default=[])
        if isinstance(raw, list):
            for entry in raw:
                if isinstance(entry, dict):
                    item_id = str(_first(entry, "id", "item_id", "ItemID", "item", default=""))
                    chance = _as_int(_first(entry, "chance", "percent", "Chance", default=100), 100)
                else:
                    item_id, chance = str(entry), 100
                if item_id and self.random.randint(1, 100) <= max(0, min(100, chance)):
                    events.append(LootEvent(event_type="item", source_id=source, item_id=item_id, notes=["Drop used explicit monster drop/list field."]))
        return events

    def generate_monster_loot(self, monster: Dict[str, Any], room_id: str = "unknown_room", create_corpse: bool = True) -> LootResult:
        source = str(_first(monster, "runtime_id", "id", "monster_id", "name", default="unknown_monster"))
        events = [self.gold_from_monster(monster)]
        events.extend(self.item_drops_from_monster(monster))
        corpse = None
        if create_corpse:
            corpse = Corpse(corpse_id=f"corpse_{source}", owner_id=source, room_id=room_id,
                            items=[e.item_id for e in events if e.item_id],
                            gold=sum(e.gold for e in events))
            events.append(LootEvent(event_type="corpse_created", source_id=source, gold=corpse.gold, notes=["Corpse lifetime is runtime foundation default, not classic claimed."]))
        return LootResult(source_id=source, events=events, corpse=corpse)

    def to_dict(self, result: LootResult) -> Dict[str, Any]:
        return asdict(result)
