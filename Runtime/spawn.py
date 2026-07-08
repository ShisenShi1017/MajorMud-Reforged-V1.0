from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Tuple, Union

from Runtime.data_runtime import DataRuntime

RoomRef = Union[int, str, Tuple[int, int]]


@dataclass(frozen=True)
class SpawnTraceStep:
    code: str
    detail: str
    data: Dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class SpawnRecord:
    room_id: str
    room_ref: Tuple[int, int]
    monster_type: int
    respawn_minutes: int
    max_area_cap: int = 0
    linked_spawn_room: int = 0
    boss_type_id: int = 0
    status: str = "SCHEDULED"


@dataclass(frozen=True)
class SpawnEvent:
    room_ref: Tuple[int, int]
    monster_type: int
    due_minute: int
    status: str
    trace: List[SpawnTraceStep]


class SpawnRuntime:
    """Deterministic spawn runtime foundation.

    Runtime-0007 indexes canonical room spawn fields and schedules spawn attempts.
    It does not choose exact monster instances, roll probabilities, enforce area caps,
    or resolve linked/boss spawn semantics until those behaviors are verified.
    """

    def __init__(self, data_runtime: DataRuntime, mode: str = "classic_unverified") -> None:
        self.data_runtime = data_runtime
        self.mode = mode
        self.rooms_by_id: Dict[str, Dict[str, Any]] = {}
        self.rooms_by_map_number: Dict[Tuple[int, int], Dict[str, Any]] = {}
        self.monsters_by_legacy: Dict[int, Dict[str, Any]] = {}
        self.spawn_records: List[SpawnRecord] = []
        self._build_indexes()

    @staticmethod
    def _as_int(value: Any, default: int = 0) -> int:
        if isinstance(value, bool):
            return int(value)
        if isinstance(value, int):
            return value
        if isinstance(value, str):
            try:
                return int(value.strip())
            except ValueError:
                return default
        return default

    @staticmethod
    def _room_number(room: Dict[str, Any]) -> Optional[int]:
        value = room.get("roomNumber")
        if isinstance(value, int):
            return value
        source_id = room.get("sourceId")
        if isinstance(source_id, dict):
            return SpawnRuntime._as_int(source_id.get("room"), 0) or None
        return None

    @staticmethod
    def _map_id(room: Dict[str, Any]) -> Optional[int]:
        value = room.get("mapId")
        if isinstance(value, int):
            return value
        source_id = room.get("sourceId")
        if isinstance(source_id, dict):
            return SpawnRuntime._as_int(source_id.get("map"), 0) or None
        return None

    def _build_indexes(self) -> None:
        for monster in self.data_runtime.get_records("monsters"):
            legacy = self._as_int(monster.get("legacyNumber") or monster.get("sourceId"), 0)
            if legacy > 0:
                self.monsters_by_legacy[legacy] = monster

        for room in self.data_runtime.get_records("rooms"):
            room_id = room.get("id")
            map_id = self._map_id(room)
            room_number = self._room_number(room)
            if isinstance(room_id, str):
                self.rooms_by_id[room_id] = room
            if map_id is not None and room_number is not None:
                self.rooms_by_map_number[(map_id, room_number)] = room
                record = self._spawn_record_for_room(room, map_id, room_number)
                if record is not None:
                    self.spawn_records.append(record)

    def _spawn_record_for_room(self, room: Dict[str, Any], map_id: int, room_number: int) -> Optional[SpawnRecord]:
        spawn = room.get("spawn")
        if not isinstance(spawn, dict):
            return None
        monster_type = self._as_int(spawn.get("monsterType"), 0)
        respawn_minutes = self._as_int(spawn.get("respawnMinutes"), 0)
        boss_type_id = self._as_int(spawn.get("bossTypeId"), 0)
        linked_spawn_room = self._as_int(spawn.get("linkedSpawnRoom"), 0)
        max_area_cap = self._as_int(spawn.get("maxAreaCap"), 0)
        if monster_type <= 0 and boss_type_id <= 0:
            return None
        return SpawnRecord(
            room_id=str(room.get("id", f"map{map_id}_room{room_number}")),
            room_ref=(map_id, room_number),
            monster_type=monster_type,
            respawn_minutes=max(respawn_minutes, 0),
            max_area_cap=max_area_cap,
            linked_spawn_room=linked_spawn_room,
            boss_type_id=boss_type_id,
        )

    def records_for_room(self, room_ref: RoomRef) -> List[SpawnRecord]:
        if isinstance(room_ref, tuple) and len(room_ref) == 2:
            wanted = (int(room_ref[0]), int(room_ref[1]))
        elif isinstance(room_ref, str):
            room = self.rooms_by_id.get(room_ref)
            if not room:
                return []
            map_id = self._map_id(room)
            room_number = self._room_number(room)
            if map_id is None or room_number is None:
                return []
            wanted = (map_id, room_number)
        else:
            return []
        return [record for record in self.spawn_records if record.room_ref == wanted]

    def schedule_initial_spawns(self, current_minute: int = 0) -> List[SpawnEvent]:
        events: List[SpawnEvent] = []
        for record in self.spawn_records:
            trace = [SpawnTraceStep("SPAWN_RECORD_INDEXED", "Canonical room spawn record indexed.", record.__dict__)]
            if record.boss_type_id > 0:
                trace.append(SpawnTraceStep("UNKNOWN_BOSS_SPAWN", "Boss spawn semantics are preserved but not resolved in Runtime-0007.", {"boss_type_id": record.boss_type_id}))
            if record.linked_spawn_room > 0:
                trace.append(SpawnTraceStep("UNKNOWN_LINKED_SPAWN", "Linked spawn behavior is preserved but not resolved in Runtime-0007.", {"linked_spawn_room": record.linked_spawn_room}))
            if record.max_area_cap > 0:
                trace.append(SpawnTraceStep("UNKNOWN_AREA_CAP", "Area cap behavior is preserved but not enforced in Runtime-0007.", {"max_area_cap": record.max_area_cap}))
            due_minute = current_minute if record.respawn_minutes == 0 else current_minute + record.respawn_minutes
            events.append(SpawnEvent(record.room_ref, record.monster_type, due_minute, "SCHEDULED", trace))
        return events

    def summary(self) -> Dict[str, Any]:
        zero_timer = sum(1 for r in self.spawn_records if r.respawn_minutes == 0)
        timed = sum(1 for r in self.spawn_records if r.respawn_minutes > 0)
        bosses = sum(1 for r in self.spawn_records if r.boss_type_id > 0)
        linked = sum(1 for r in self.spawn_records if r.linked_spawn_room > 0)
        area_capped = sum(1 for r in self.spawn_records if r.max_area_cap > 0)
        missing_monster_refs = sum(1 for r in self.spawn_records if r.monster_type > 0 and r.monster_type not in self.monsters_by_legacy)
        return {
            "rooms_indexed": len(self.rooms_by_map_number),
            "monsters_indexed": len(self.monsters_by_legacy),
            "spawn_records": len(self.spawn_records),
            "timed_spawn_records": timed,
            "zero_timer_spawn_records": zero_timer,
            "boss_spawn_records": bosses,
            "linked_spawn_records": linked,
            "area_capped_spawn_records": area_capped,
            "missing_monster_refs": missing_monster_refs,
            "mode": self.mode,
        }
