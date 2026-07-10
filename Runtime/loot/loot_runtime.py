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
        self.loot_by_monster_id: Dict[str, List[Dict[str, Any]]] = {}
        self.loot_by_monster_legacy: Dict[str, List[Dict[str, Any]]] = {}
        for table in self.loot_tables:
            if not isinstance(table, dict):
                continue
            entries = table.get("entries") or []
            if not isinstance(entries, list):
                continue
            monster_id = _first(table, "monster_runtime_id", "monsterRuntimeId", "monsterId", "monster_id")
            monster_legacy = _first(table, "monster_legacy_number", "monsterLegacyNumber", "monsterLegacyId", "monster_id")
            if monster_id not in (None, ""):
                self.loot_by_monster_id.setdefault(str(monster_id), []).extend([e for e in entries if isinstance(e, dict)])
            if monster_legacy not in (None, ""):
                self.loot_by_monster_legacy.setdefault(str(monster_legacy), []).extend([e for e in entries if isinstance(e, dict)])

    @staticmethod
    def drop_roll_succeeds(drop_percent: Any, roll: int) -> bool:
        chance = max(0, min(100, _as_int(drop_percent, 0)))
        if chance <= 0:
            return False
        return 1 <= roll <= 100 and roll <= chance

    def roll_drop_percent(self, drop_percent: Any) -> bool:
        # Classic kill-time loot uses a full-width integer 1..100 bucket.
        # Do not round a scaled float; rounded endpoints halve the 1 and 100 buckets.
        return self.drop_roll_succeeds(drop_percent, self.random.randint(1, 100))

    def _loot_entries_for_monster(self, monster: Dict[str, Any]) -> List[Dict[str, Any]]:
        entries: List[Dict[str, Any]] = []
        raw = _first(monster, "items", "drops", "loot", "Treasure", "treasure", default=[])
        if isinstance(raw, list):
            for entry in raw:
                if isinstance(entry, dict):
                    entries.append(entry)
                else:
                    entries.append({"item_id": entry, "drop_percent": 100})

        monster_ids = [
            _first(monster, "runtime_id", "id", "monster_id", "monsterId"),
            _first(monster, "legacy_number", "legacyNumber", "legacy_id", "legacyId", "sourceId"),
        ]
        for monster_id in monster_ids:
            if monster_id in (None, ""):
                continue
            key = str(monster_id)
            entries.extend(self.loot_by_monster_id.get(key, []))
            entries.extend(self.loot_by_monster_legacy.get(key, []))
        return entries

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
        for entry in self._loot_entries_for_monster(monster):
            item_id = str(_first(entry, "item_runtime_id", "itemRuntimeId", "id", "item_id", "ItemID", "item", default=""))
            chance = _first(entry, "drop_percent", "dropPercent", "chance", "percent", "Chance", default=100)
            if item_id and self.roll_drop_percent(chance):
                events.append(LootEvent(event_type="item", source_id=source, item_id=item_id, notes=["Drop used Classic full-width 1..100 integer percent roll."]))
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
