
"""MajorMUD Reforged Runtime-0014 Persistence / Save-Load.

This module adds deterministic JSON persistence for the offline runtime. It is
explicitly a runtime-state serializer, not a claim about classic MajorMUD save
file layout. Classic binary/user-record interpretation remains evidence-gated.

Persisted surfaces:
- character/runtime entity state
- inventory and equipment state
- ability grants and quest progress blocks
- world state, room occupants, corpses, ground items, spawn timers, combat pairs
- save envelope metadata and schema version
"""
from __future__ import annotations

from dataclasses import asdict, dataclass, field, is_dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional
import hashlib
import json
import os
import re
import tempfile

try:
    from Runtime.world.world_state import WorldState, RuntimeEntity, GroundItem, RuntimeEvent
except Exception:  # pragma: no cover
    from ..world.world_state import WorldState, RuntimeEntity, GroundItem, RuntimeEvent

SAVE_SCHEMA_VERSION = "major-mud-reforged-save-v0014"


class PersistenceError(RuntimeError):
    """Raised when persistence validation or IO fails."""


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def stable_json(data: Any) -> str:
    return json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False)


def sha256_json(data: Any) -> str:
    return hashlib.sha256(stable_json(data).encode("utf-8")).hexdigest()


def safe_id(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", str(value).strip())
    return cleaned or "unknown"


def _jsonable(value: Any) -> Any:
    if is_dataclass(value):
        return asdict(value)
    if isinstance(value, dict):
        return {str(k): _jsonable(v) for k, v in value.items()}
    if isinstance(value, (list, tuple, set)):
        return [_jsonable(v) for v in value]
    return value


@dataclass
class SaveEnvelope:
    save_type: str
    payload: Dict[str, Any]
    entity_id: Optional[str] = None
    world_id: Optional[str] = None
    schema_version: str = SAVE_SCHEMA_VERSION
    created_at: str = field(default_factory=utc_now)
    classicAccuracy: str = "reforged_runtime_persistence_not_classic_binary_claim"
    metadata: Dict[str, Any] = field(default_factory=dict)
    checksum: str = ""

    def to_dict(self) -> Dict[str, Any]:
        base = {
            "schema_version": self.schema_version,
            "save_type": self.save_type,
            "entity_id": self.entity_id,
            "world_id": self.world_id,
            "created_at": self.created_at,
            "classicAccuracy": self.classicAccuracy,
            "metadata": self.metadata,
            "payload": self.payload,
        }
        checksum = self.checksum or sha256_json(base)
        base["checksum"] = checksum
        return base

    @staticmethod
    def from_dict(data: Dict[str, Any], verify_checksum: bool = True) -> "SaveEnvelope":
        if data.get("schema_version") != SAVE_SCHEMA_VERSION:
            raise PersistenceError(f"Unsupported save schema: {data.get('schema_version')}")
        checksum = data.get("checksum") or ""
        if verify_checksum:
            unsigned = dict(data)
            unsigned.pop("checksum", None)
            expected = sha256_json(unsigned)
            if checksum != expected:
                raise PersistenceError("Save checksum mismatch")
        return SaveEnvelope(
            schema_version=data.get("schema_version", SAVE_SCHEMA_VERSION),
            save_type=str(data.get("save_type") or "unknown"),
            entity_id=data.get("entity_id"),
            world_id=data.get("world_id"),
            created_at=str(data.get("created_at") or utc_now()),
            classicAccuracy=str(data.get("classicAccuracy") or "reforged_runtime_persistence_not_classic_binary_claim"),
            metadata=dict(data.get("metadata") or {}),
            payload=dict(data.get("payload") or {}),
            checksum=checksum,
        )


class SaveStore:
    """Atomic JSON save store rooted inside /Saves by default."""

    def __init__(self, root: Path | str):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)

    def write_envelope(self, relative_path: str | Path, envelope: SaveEnvelope) -> Path:
        path = self.root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        data = stable_json(envelope.to_dict())
        fd, tmp_name = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=str(path.parent))
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as fh:
                fh.write(data)
                fh.write("\n")
            os.replace(tmp_name, path)
        finally:
            if os.path.exists(tmp_name):
                os.unlink(tmp_name)
        return path

    def read_envelope(self, relative_path: str | Path, verify_checksum: bool = True) -> SaveEnvelope:
        path = self.root / relative_path
        if not path.exists():
            raise PersistenceError(f"Save file not found: {path}")
        data = json.loads(path.read_text(encoding="utf-8"))
        return SaveEnvelope.from_dict(data, verify_checksum=verify_checksum)

    def list_saves(self, subdir: str = "") -> List[str]:
        base = self.root / subdir
        if not base.exists():
            return []
        return sorted(str(p.relative_to(self.root)) for p in base.rglob("*.json"))


class CharacterSaveCodec:
    @staticmethod
    def serialize(entity: RuntimeEntity, ability_state: Any = None, quest_state: Any = None) -> Dict[str, Any]:
        payload = _jsonable(entity)
        payload["persistence_blocks"] = {
            "abilities": ability_state.serialize() if ability_state is not None and hasattr(ability_state, "serialize") else {},
            "quests": quest_state.serialize() if quest_state is not None and hasattr(quest_state, "serialize") else {},
        }
        payload["trace"] = {
            "event": "character_serialized",
            "schema_version": SAVE_SCHEMA_VERSION,
            "classicAccuracy": "runtime_state_only_not_classic_user_record_claim",
        }
        return payload

    @staticmethod
    def deserialize(payload: Dict[str, Any]) -> RuntimeEntity:
        allowed = {
            "runtime_id", "name", "entity_type", "room_ref", "hp", "max_hp", "mana", "max_mana",
            "cp", "max_cp", "level", "stats", "skills", "equipment_totals", "inventory",
            "equipment", "flags",
        }
        kwargs = {k: payload.get(k) for k in allowed if k in payload}
        kwargs.setdefault("runtime_id", str(payload.get("id") or "loaded_player"))
        kwargs.setdefault("name", str(payload.get("name") or kwargs["runtime_id"]))
        kwargs.setdefault("entity_type", str(payload.get("entity_type") or "player"))
        kwargs.setdefault("hp", int(payload.get("hp") or 1))
        kwargs.setdefault("max_hp", int(payload.get("max_hp") or kwargs["hp"]))
        kwargs.setdefault("mana", int(payload.get("mana") or 0))
        kwargs.setdefault("max_mana", int(payload.get("max_mana") or kwargs["mana"]))
        kwargs.setdefault("cp", int(payload.get("cp") or 0))
        kwargs.setdefault("max_cp", int(payload.get("max_cp") or kwargs["cp"]))
        kwargs.setdefault("level", int(payload.get("level") or 1))
        kwargs.setdefault("stats", dict(payload.get("stats") or {}))
        kwargs.setdefault("skills", dict(payload.get("skills") or {}))
        kwargs.setdefault("equipment_totals", dict(payload.get("equipment_totals") or {}))
        kwargs.setdefault("inventory", list(payload.get("inventory") or []))
        kwargs.setdefault("equipment", dict(payload.get("equipment") or {}))
        kwargs.setdefault("flags", dict(payload.get("flags") or {}))
        return RuntimeEntity(**kwargs)


class WorldSaveCodec:
    @staticmethod
    def serialize(world: WorldState) -> Dict[str, Any]:
        payload = world.to_jsonable()
        payload["status_effects"] = _jsonable(getattr(world, "status_effects", []))
        payload["spawn_timers"] = dict(getattr(world, "spawn_timers", {}) or {})
        payload["trace"] = {
            "event": "world_serialized",
            "schema_version": SAVE_SCHEMA_VERSION,
            "classicAccuracy": "runtime_world_state_only",
        }
        return payload

    @staticmethod
    def deserialize(payload: Dict[str, Any]) -> WorldState:
        world = WorldState(
            world_id=str(payload.get("world_id") or "loaded_world"),
            tick=int(payload.get("tick") or 0),
            current_room=payload.get("current_room"),
            corpses=list(payload.get("corpses") or []),
            spawn_timers=dict(payload.get("spawn_timers") or {}),
            combat_pairs=dict(payload.get("combat_pairs") or {}),
            flags=dict(payload.get("flags") or {}),
        )
        for rid, rec in dict(payload.get("players") or {}).items():
            world.players[str(rid)] = CharacterSaveCodec.deserialize(rec)
        for rid, rec in dict(payload.get("monsters") or {}).items():
            world.monsters[str(rid)] = CharacterSaveCodec.deserialize({**rec, "entity_type": rec.get("entity_type", "monster")})
        for rid, rec in dict(payload.get("npcs") or {}).items():
            world.npcs[str(rid)] = CharacterSaveCodec.deserialize({**rec, "entity_type": rec.get("entity_type", "npc")})
        for rec in list(payload.get("ground_items") or []):
            world.ground_items.append(GroundItem(**rec))
        for rec in list(payload.get("events") or []):
            world.events.append(RuntimeEvent(**rec))
        world.status_effects = list(payload.get("status_effects") or [])
        return world


class PersistenceRuntime:
    def __init__(self, save_root: Path | str):
        self.store = SaveStore(save_root)
        self.trace: List[Dict[str, Any]] = []

    def save_character(self, entity: RuntimeEntity, ability_state: Any = None, quest_state: Any = None) -> Path:
        payload = CharacterSaveCodec.serialize(entity, ability_state=ability_state, quest_state=quest_state)
        envelope = SaveEnvelope(save_type="character", entity_id=entity.runtime_id, payload=payload)
        path = self.store.write_envelope(Path("characters") / f"{safe_id(entity.runtime_id)}.json", envelope)
        self.trace.append({"event": "character_saved", "entity_id": entity.runtime_id, "path": str(path)})
        return path

    def load_character(self, entity_id: str) -> RuntimeEntity:
        envelope = self.store.read_envelope(Path("characters") / f"{safe_id(entity_id)}.json")
        if envelope.save_type != "character":
            raise PersistenceError("Save file is not a character save")
        entity = CharacterSaveCodec.deserialize(envelope.payload)
        self.trace.append({"event": "character_loaded", "entity_id": entity.runtime_id})
        return entity

    def save_world(self, world: WorldState) -> Path:
        payload = WorldSaveCodec.serialize(world)
        envelope = SaveEnvelope(save_type="world", world_id=world.world_id, payload=payload)
        path = self.store.write_envelope(Path("worlds") / f"{safe_id(world.world_id)}.json", envelope)
        self.trace.append({"event": "world_saved", "world_id": world.world_id, "path": str(path)})
        return path

    def load_world(self, world_id: str) -> WorldState:
        envelope = self.store.read_envelope(Path("worlds") / f"{safe_id(world_id)}.json")
        if envelope.save_type != "world":
            raise PersistenceError("Save file is not a world save")
        world = WorldSaveCodec.deserialize(envelope.payload)
        self.trace.append({"event": "world_loaded", "world_id": world.world_id})
        return world

    def save_snapshot(self, snapshot_id: str, world: WorldState, characters: Optional[Iterable[RuntimeEntity]] = None) -> Path:
        payload = {
            "world": WorldSaveCodec.serialize(world),
            "characters": [CharacterSaveCodec.serialize(c) for c in (characters or world.players.values())],
        }
        envelope = SaveEnvelope(save_type="runtime_snapshot", world_id=world.world_id, entity_id=snapshot_id, payload=payload)
        path = self.store.write_envelope(Path("snapshots") / f"{safe_id(snapshot_id)}.json", envelope)
        self.trace.append({"event": "snapshot_saved", "snapshot_id": snapshot_id, "path": str(path)})
        return path
