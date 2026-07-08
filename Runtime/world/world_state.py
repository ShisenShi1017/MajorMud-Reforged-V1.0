from __future__ import annotations

from dataclasses import dataclass, field, asdict
from typing import Any, Dict, List, Optional


@dataclass
class RuntimeEntity:
    runtime_id: str
    name: str
    entity_type: str
    room_ref: Any = None
    hp: int = 10
    max_hp: int = 10
    mana: int = 0
    max_mana: int = 0
    cp: int = 0
    max_cp: int = 0
    level: int = 1
    stats: Dict[str, int] = field(default_factory=dict)
    skills: Dict[str, int] = field(default_factory=dict)
    equipment_totals: Dict[str, int] = field(default_factory=dict)
    inventory: List[Dict[str, Any]] = field(default_factory=list)
    equipment: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    flags: Dict[str, Any] = field(default_factory=dict)

    @property
    def alive(self) -> bool:
        return self.hp > 0

    def to_actor_dict(self) -> Dict[str, Any]:
        return {
            "runtime_id": self.runtime_id,
            "id": self.runtime_id,
            "name": self.name,
            "level": self.level,
            "hp": self.hp,
            "mana": self.mana,
            "cp": self.cp,
            **self.stats,
            **self.skills,
            **self.equipment_totals,
            **self.flags,
        }


@dataclass
class GroundItem:
    item_id: str
    name: str
    room_ref: Any
    quantity: int = 1
    data: Dict[str, Any] = field(default_factory=dict)


@dataclass
class RuntimeEvent:
    event_type: str
    tick: int
    actor_id: Optional[str] = None
    target_id: Optional[str] = None
    message: str = ""
    data: Dict[str, Any] = field(default_factory=dict)


@dataclass
class WorldState:
    """Single authoritative in-memory state for the offline runtime kernel.

    This is not persistence or networking. Runtime-0012 keeps the world state small,
    deterministic, and serializable so later server/persistence milestones can wrap it.
    """

    world_id: str = "runtime_test_world"
    tick: int = 0
    current_room: Any = None
    players: Dict[str, RuntimeEntity] = field(default_factory=dict)
    monsters: Dict[str, RuntimeEntity] = field(default_factory=dict)
    npcs: Dict[str, RuntimeEntity] = field(default_factory=dict)
    ground_items: List[GroundItem] = field(default_factory=list)
    corpses: List[Dict[str, Any]] = field(default_factory=list)
    events: List[RuntimeEvent] = field(default_factory=list)
    spawn_timers: Dict[str, int] = field(default_factory=dict)
    combat_pairs: Dict[str, str] = field(default_factory=dict)
    status_effects: List[Any] = field(default_factory=list)
    flags: Dict[str, Any] = field(default_factory=dict)

    def add_event(self, event_type: str, actor_id: Optional[str] = None, target_id: Optional[str] = None, message: str = "", data: Optional[Dict[str, Any]] = None) -> RuntimeEvent:
        event = RuntimeEvent(event_type=event_type, tick=self.tick, actor_id=actor_id, target_id=target_id, message=message, data=data or {})
        self.events.append(event)
        return event

    def add_player(self, entity: RuntimeEntity) -> RuntimeEntity:
        self.players[entity.runtime_id] = entity
        if self.current_room is None:
            self.current_room = entity.room_ref
        self.add_event("player_added", entity.runtime_id, message=f"{entity.name} enters the runtime.")
        return entity

    def add_monster(self, entity: RuntimeEntity) -> RuntimeEntity:
        self.monsters[entity.runtime_id] = entity
        self.add_event("monster_added", entity.runtime_id, message=f"{entity.name} is present.")
        return entity

    def get_entity(self, runtime_id: str) -> Optional[RuntimeEntity]:
        return self.players.get(runtime_id) or self.monsters.get(runtime_id) or self.npcs.get(runtime_id)

    def entities_in_room(self, room_ref: Any, include_dead: bool = False) -> List[RuntimeEntity]:
        entities = list(self.players.values()) + list(self.monsters.values()) + list(self.npcs.values())
        return [e for e in entities if e.room_ref == room_ref and (include_dead or e.alive)]

    def monsters_in_room(self, room_ref: Any) -> List[RuntimeEntity]:
        return [m for m in self.monsters.values() if m.room_ref == room_ref and m.alive]

    def resolve_target(self, actor_id: str, query: str) -> Optional[RuntimeEntity]:
        actor = self.get_entity(actor_id)
        if actor is None:
            return None
        wanted = query.strip().lower()
        candidates = self.entities_in_room(actor.room_ref)
        for entity in candidates:
            if entity.runtime_id == actor_id:
                continue
            name = entity.name.lower()
            if entity.runtime_id.lower() == wanted or name == wanted or name.startswith(wanted) or wanted in name:
                return entity
        return None

    def items_in_room(self, room_ref: Any) -> List[GroundItem]:
        return [item for item in self.ground_items if item.room_ref == room_ref]

    def find_ground_item(self, room_ref: Any, query: str) -> Optional[GroundItem]:
        wanted = query.strip().lower()
        for item in self.items_in_room(room_ref):
            if item.item_id.lower() == wanted or item.name.lower() == wanted or item.name.lower().startswith(wanted):
                return item
        return None

    def remove_ground_item(self, item: GroundItem) -> None:
        self.ground_items.remove(item)

    def to_jsonable(self) -> Dict[str, Any]:
        return {
            "world_id": self.world_id,
            "tick": self.tick,
            "players": {k: asdict(v) for k, v in self.players.items()},
            "monsters": {k: asdict(v) for k, v in self.monsters.items()},
            "npcs": {k: asdict(v) for k, v in self.npcs.items()},
            "ground_items": [asdict(i) for i in self.ground_items],
            "corpses": self.corpses,
            "events": [asdict(e) for e in self.events],
            "combat_pairs": self.combat_pairs,
            "flags": self.flags,
        }
