"""Canonical runtime schemas.

These schemas intentionally preserve raw records alongside normalized runtime fields.
They do not claim classic formula accuracy; they only make canonical /Data usable by
runtime and simulator systems.
"""
from __future__ import annotations
from dataclasses import dataclass, field, asdict
from typing import Any, Dict, List, Optional

@dataclass
class SchemaIssue:
    severity: str
    category: str
    record_type: str
    record_id: str
    message: str

@dataclass
class RoomExitSchema:
    direction: str
    target_room_number: Optional[int]
    target_runtime_id: Optional[str]
    exit_type: Optional[int]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class RoomSchema:
    runtime_id: str
    legacy_room_number: Optional[int]
    map_id: Optional[int]
    name: str
    description: str
    exits: List[RoomExitSchema]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class ItemSchema:
    runtime_id: str
    legacy_number: Optional[int]
    name: str
    item_type: str
    combat: Dict[str, Any]
    defense: Dict[str, Any]
    economy: Dict[str, Any]
    restrictions: Dict[str, Any]
    properties: Dict[str, Any]
    uses: Dict[str, Any]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class MonsterAttackSchema:
    attack_type: Optional[int]
    min_hit: int
    max_hit: int
    percent: int
    accuracy: int
    hit_spell: Optional[int]
    energy_cost: int
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class MonsterSchema:
    runtime_id: str
    legacy_number: Optional[int]
    name: str
    level: int
    stats: Dict[str, Any]
    experience: Dict[str, Any]
    behavior: Dict[str, Any]
    attacks: List[MonsterAttackSchema]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class SpellSchema:
    runtime_id: str
    legacy_number: Optional[int]
    name: str
    level: Optional[int]
    spell_type: Optional[Any]
    cost: Optional[Any]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class ShopInventorySchema:
    item_legacy_number: Optional[int]
    item_runtime_id: Optional[str]
    max_qty: Optional[int]
    regen_qty: Optional[int]
    normal_qty: Optional[int]
    regen_time: Optional[int]
    regen_percent: Optional[int]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class ShopSchema:
    runtime_id: str
    legacy_number: Optional[int]
    name: str
    shop_type: Optional[int]
    markup_percent: Optional[int]
    inventory: List[ShopInventorySchema]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class LootEntrySchema:
    name: Optional[str]
    item_legacy_number: Optional[int]
    item_runtime_id: Optional[str]
    drop_percent: Optional[int]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class LootTableSchema:
    runtime_id: str
    monster_legacy_number: Optional[int]
    monster_runtime_id: Optional[str]
    entries: List[LootEntrySchema]
    raw: Dict[str, Any] = field(default_factory=dict)

@dataclass
class SchemaBundle:
    rooms: Dict[str, RoomSchema]
    items: Dict[str, ItemSchema]
    monsters: Dict[str, MonsterSchema]
    spells: Dict[str, SpellSchema]
    shops: Dict[str, ShopSchema]
    loot_tables: Dict[str, LootTableSchema]
    issues: List[SchemaIssue]

    def counts(self) -> Dict[str, int]:
        return {
            "rooms": len(self.rooms),
            "items": len(self.items),
            "monsters": len(self.monsters),
            "spells": len(self.spells),
            "shops": len(self.shops),
            "loot_tables": len(self.loot_tables),
            "issues": len(self.issues),
        }

    def to_jsonable(self) -> Dict[str, Any]:
        return asdict(self)
