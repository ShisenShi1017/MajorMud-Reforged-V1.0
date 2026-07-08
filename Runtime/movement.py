from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Tuple, Union

from Runtime.data_runtime import DataRuntime

NORMAL_EXIT_TYPES = {None, 0}
DIRECTION_ALIASES = {
    "N": "N", "NORTH": "N", "S": "S", "SOUTH": "S", "E": "E", "EAST": "E", "W": "W", "WEST": "W",
    "U": "U", "UP": "U", "D": "D", "DOWN": "D", "NE": "NE", "NORTHEAST": "NE", "NW": "NW", "NORTHWEST": "NW",
    "SE": "SE", "SOUTHEAST": "SE", "SW": "SW", "SOUTHWEST": "SW",
}
RoomRef = Union[int, str, Tuple[int, int]]

@dataclass(frozen=True)
class MovementTraceStep:
    code: str
    detail: str
    data: Dict[str, Any] = field(default_factory=dict)

@dataclass(frozen=True)
class MovementResult:
    status: str
    from_room: Optional[RoomRef]
    to_room: Optional[RoomRef]
    direction: str
    mode: str
    trace: List[MovementTraceStep]

    @property
    def moved(self) -> bool:
        return self.status == "MOVED"

class MovementRuntime:
    """Deterministic, map-aware movement foundation.

    Runtime-0006 implements only safe traversal behavior. Normal/open exits may move.
    Special exit behavior is preserved as UNKNOWN_SPECIAL_EXIT unless explicitly proven later.
    """

    def __init__(self, data_runtime: DataRuntime, mode: str = "classic_unverified") -> None:
        self.data_runtime = data_runtime
        self.mode = mode
        self.rooms_by_id: Dict[str, Dict[str, Any]] = {}
        self.rooms_by_map_number: Dict[Tuple[int, int], Dict[str, Any]] = {}
        self.unique_room_numbers: Dict[int, Dict[str, Any]] = {}
        self.ambiguous_room_numbers: set[int] = set()
        self._build_indexes()

    def _build_indexes(self) -> None:
        seen_numbers: Dict[int, Dict[str, Any]] = {}
        for room in self.data_runtime.get_records("rooms"):
            room_id = room.get("id")
            if isinstance(room_id, str):
                self.rooms_by_id[room_id] = room
            room_number = self._room_number(room)
            map_id = self._map_id(room)
            if room_number is not None and map_id is not None:
                self.rooms_by_map_number[(map_id, room_number)] = room
            if room_number is not None:
                if room_number in seen_numbers:
                    self.ambiguous_room_numbers.add(room_number)
                else:
                    seen_numbers[room_number] = room
        self.unique_room_numbers = {n: r for n, r in seen_numbers.items() if n not in self.ambiguous_room_numbers}

    @staticmethod
    def normalize_direction(direction: str) -> str:
        return DIRECTION_ALIASES.get(str(direction).strip().upper(), str(direction).strip().upper())

    @staticmethod
    def _room_number(room: Dict[str, Any]) -> Optional[int]:
        for key in ("roomNumber", "room", "Room", "number", "Number"):
            value = room.get(key)
            if isinstance(value, int):
                return value
            if isinstance(value, str) and value.isdigit():
                return int(value)
        source_id = room.get("sourceId")
        if isinstance(source_id, dict):
            value = source_id.get("room")
            if isinstance(value, int):
                return value
            if isinstance(value, str) and value.isdigit():
                return int(value)
        return None

    @staticmethod
    def _map_id(room: Dict[str, Any]) -> Optional[int]:
        value = room.get("mapId")
        if isinstance(value, int):
            return value
        if isinstance(value, str) and value.isdigit():
            return int(value)
        source_id = room.get("sourceId")
        if isinstance(source_id, dict):
            value = source_id.get("map")
            if isinstance(value, int):
                return value
            if isinstance(value, str) and value.isdigit():
                return int(value)
        return None

    def get_room(self, room_ref: RoomRef, map_id: Optional[int] = None) -> Optional[Dict[str, Any]]:
        if isinstance(room_ref, str):
            return self.rooms_by_id.get(room_ref)
        if isinstance(room_ref, tuple) and len(room_ref) == 2:
            return self.rooms_by_map_number.get((int(room_ref[0]), int(room_ref[1])))
        if isinstance(room_ref, int):
            if map_id is not None:
                return self.rooms_by_map_number.get((int(map_id), int(room_ref)))
            return self.unique_room_numbers.get(int(room_ref))
        return None

    def canonical_ref(self, room: Dict[str, Any]) -> Optional[Tuple[int, int]]:
        map_id = self._map_id(room)
        room_number = self._room_number(room)
        if map_id is None or room_number is None:
            return None
        return (map_id, room_number)

    def exits_for_room(self, room_ref: RoomRef, map_id: Optional[int] = None) -> List[Dict[str, Any]]:
        room = self.get_room(room_ref, map_id=map_id)
        if not room:
            return []
        exits = room.get("exits", [])
        return [x for x in exits if isinstance(x, dict)]

    def find_exit(self, room_ref: RoomRef, direction: str, map_id: Optional[int] = None) -> Optional[Dict[str, Any]]:
        wanted = self.normalize_direction(direction)
        for exit_record in self.exits_for_room(room_ref, map_id=map_id):
            if self.normalize_direction(exit_record.get("dir", "")) == wanted:
                return exit_record
        return None

    def can_traverse(self, room_ref: RoomRef, direction: str, map_id: Optional[int] = None) -> MovementResult:
        trace: List[MovementTraceStep] = []
        normalized = self.normalize_direction(direction)
        room = self.get_room(room_ref, map_id=map_id)
        if room is None:
            trace.append(MovementTraceStep("ROOM_NOT_FOUND", "Source room does not exist or numeric room number is ambiguous.", {"room": room_ref, "map_id": map_id}))
            return MovementResult("ROOM_NOT_FOUND", room_ref, None, normalized, self.mode, trace)

        source_ref = self.canonical_ref(room) or room_ref
        source_map = self._map_id(room)
        exit_record = self.find_exit(source_ref if isinstance(source_ref, tuple) else room_ref, normalized)
        if exit_record is None:
            trace.append(MovementTraceStep("NO_EXIT", "No exit exists in requested direction.", {"room": source_ref, "direction": normalized}))
            return MovementResult("NO_EXIT", source_ref, None, normalized, self.mode, trace)

        target_number = exit_record.get("to_room")
        if not isinstance(target_number, int):
            trace.append(MovementTraceStep("EXIT_TARGET_INVALID", "Exit target is missing or not numeric.", {"exit": exit_record}))
            return MovementResult("EXIT_TARGET_INVALID", source_ref, None, normalized, self.mode, trace)

        target_ref: RoomRef = (source_map, target_number) if source_map is not None else target_number
        if self.get_room(target_ref) is None:
            trace.append(MovementTraceStep("EXIT_TARGET_MISSING", "Exit target room is not present for the source map in canonical rooms data.", {"target": target_ref}))
            return MovementResult("EXIT_TARGET_MISSING", source_ref, target_ref, normalized, self.mode, trace)

        exit_type = exit_record.get("exit_type")
        if exit_type not in NORMAL_EXIT_TYPES:
            trace.append(MovementTraceStep("UNKNOWN_SPECIAL_EXIT", "Special exit behavior is preserved but not resolved by Runtime-0006.", {"exit_type": exit_type, "exit": exit_record}))
            return MovementResult("UNKNOWN_SPECIAL_EXIT", source_ref, target_ref, normalized, self.mode, trace)

        trace.append(MovementTraceStep("OPEN_EXIT", "Normal exit may be traversed.", {"to_room": target_ref, "exit": exit_record}))
        return MovementResult("MOVED", source_ref, target_ref, normalized, self.mode, trace)

    def move(self, room_ref: RoomRef, direction: str, map_id: Optional[int] = None) -> MovementResult:
        return self.can_traverse(room_ref, direction, map_id=map_id)

    def graph_summary(self) -> Dict[str, Any]:
        rooms = self.data_runtime.get_records("rooms")
        total_exits = open_exits = special_exits = missing_targets = 0
        for room in rooms:
            source_map = self._map_id(room)
            for exit_record in room.get("exits", []) if isinstance(room.get("exits"), list) else []:
                if not isinstance(exit_record, dict):
                    continue
                total_exits += 1
                target = exit_record.get("to_room")
                target_ref = (source_map, target) if isinstance(source_map, int) and isinstance(target, int) else target
                if isinstance(target, int) and self.get_room(target_ref) is None:
                    missing_targets += 1
                if exit_record.get("exit_type") in NORMAL_EXIT_TYPES:
                    open_exits += 1
                else:
                    special_exits += 1
        return {
            "rooms_indexed": len(self.rooms_by_map_number),
            "unique_numeric_room_numbers": len(self.unique_room_numbers),
            "ambiguous_numeric_room_numbers": len(self.ambiguous_room_numbers),
            "total_exits": total_exits,
            "open_exits": open_exits,
            "special_or_unresolved_exits": special_exits,
            "missing_targets": missing_targets,
            "mode": self.mode,
        }
