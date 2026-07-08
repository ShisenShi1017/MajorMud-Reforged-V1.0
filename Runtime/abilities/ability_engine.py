"""MajorMUD Reforged Runtime-0013B Ability Engine.

This module treats MajorMUD ability flags as first-class runtime state.
It does not claim hidden classic behavior. It provides deterministic flag
storage, grant/revoke/query, provenance traces, and hooks for quests/items/
classes/races/scripts to attach ability flags safely.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any, Dict, Iterable, List, Optional, Set


@dataclass(frozen=True)
class AbilityDefinition:
    ability_id: str
    legacy_number: Optional[int]
    name: str
    description: str = ""
    implementation_status: str = "unknown"
    source: Dict[str, Any] = field(default_factory=dict)

    @staticmethod
    def from_record(record: Dict[str, Any]) -> "AbilityDefinition":
        return AbilityDefinition(
            ability_id=str(record.get("id") or record.get("ability_id") or record.get("name") or "unknown"),
            legacy_number=record.get("legacyNumber"),
            name=str(record.get("name") or record.get("id") or "Unnamed Ability"),
            description=str(record.get("description") or ""),
            implementation_status=str(record.get("implementationStatus") or "unknown"),
            source=record.get("source") or {},
        )


@dataclass
class AbilityGrant:
    ability_id: str
    source_type: str
    source_id: str
    scope: str = "permanent"  # permanent, temporary, quest, item, class, race, script
    stacks: int = 1
    expires_at_tick: Optional[int] = None
    granted_at: str = field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    metadata: Dict[str, Any] = field(default_factory=dict)

    def active_at(self, tick: Optional[int] = None) -> bool:
        return self.expires_at_tick is None or tick is None or tick < self.expires_at_tick


class AbilityCatalog:
    def __init__(self, definitions: Iterable[AbilityDefinition]):
        self.by_id: Dict[str, AbilityDefinition] = {d.ability_id: d for d in definitions}
        self.by_legacy: Dict[int, AbilityDefinition] = {
            int(d.legacy_number): d for d in definitions if d.legacy_number is not None
        }
        self.by_name: Dict[str, AbilityDefinition] = {d.name.lower(): d for d in definitions}

    @classmethod
    def from_records(cls, records: Iterable[Dict[str, Any]]) -> "AbilityCatalog":
        return cls(AbilityDefinition.from_record(r) for r in records)

    def resolve(self, ability_ref: Any) -> Optional[AbilityDefinition]:
        if isinstance(ability_ref, int):
            return self.by_legacy.get(ability_ref)
        key = str(ability_ref)
        return self.by_id.get(key) or self.by_name.get(key.lower())

    def unknown_refs(self, refs: Iterable[Any]) -> List[Any]:
        return [r for r in refs if self.resolve(r) is None]


class AbilityState:
    def __init__(self) -> None:
        self._grants: Dict[str, List[AbilityGrant]] = {}
        self.trace: List[Dict[str, Any]] = []

    def grant(self, grant: AbilityGrant) -> Dict[str, Any]:
        self._grants.setdefault(grant.ability_id, []).append(grant)
        event = {
            "event": "ability_granted",
            "ability_id": grant.ability_id,
            "source_type": grant.source_type,
            "source_id": grant.source_id,
            "scope": grant.scope,
            "stacks": grant.stacks,
            "classicAccuracy": "state_tracking_only_not_formula_claimed",
        }
        self.trace.append(event)
        return event

    def revoke(self, ability_id: str, source_type: Optional[str] = None, source_id: Optional[str] = None) -> Dict[str, Any]:
        prior = self._grants.get(ability_id, [])
        kept: List[AbilityGrant] = []
        removed = 0
        for grant in prior:
            if source_type is not None and grant.source_type != source_type:
                kept.append(grant); continue
            if source_id is not None and grant.source_id != source_id:
                kept.append(grant); continue
            removed += 1
        if kept:
            self._grants[ability_id] = kept
        else:
            self._grants.pop(ability_id, None)
        event = {"event": "ability_revoked", "ability_id": ability_id, "removed": removed}
        self.trace.append(event)
        return event

    def has(self, ability_id: str, tick: Optional[int] = None) -> bool:
        return any(g.active_at(tick) for g in self._grants.get(ability_id, []))

    def stack_count(self, ability_id: str, tick: Optional[int] = None) -> int:
        return sum(max(0, g.stacks) for g in self._grants.get(ability_id, []) if g.active_at(tick))

    def active_ids(self, tick: Optional[int] = None) -> Set[str]:
        return {aid for aid in self._grants if self.has(aid, tick)}

    def expire(self, tick: int) -> List[Dict[str, Any]]:
        events: List[Dict[str, Any]] = []
        for aid in list(self._grants.keys()):
            active = [g for g in self._grants[aid] if g.active_at(tick)]
            expired = len(self._grants[aid]) - len(active)
            if active:
                self._grants[aid] = active
            else:
                self._grants.pop(aid, None)
            if expired:
                event = {"event": "ability_expired", "ability_id": aid, "expired": expired, "tick": tick}
                self.trace.append(event)
                events.append(event)
        return events

    def serialize(self) -> Dict[str, Any]:
        return {"grants": {aid: [g.__dict__ for g in grants] for aid, grants in self._grants.items()}, "trace": self.trace}


def build_ability_requirement(requirements: Iterable[Any], catalog: AbilityCatalog) -> Dict[str, Any]:
    refs = list(requirements or [])
    resolved = [catalog.resolve(r) for r in refs]
    return {
        "required_refs": refs,
        "required_ids": [d.ability_id for d in resolved if d],
        "unknown_refs": [refs[i] for i, d in enumerate(resolved) if d is None],
        "classicAccuracy": "requirement_mapping_only",
    }


def check_ability_requirement(state: AbilityState, required_ids: Iterable[str], tick: Optional[int] = None) -> Dict[str, Any]:
    required = list(required_ids or [])
    missing = [aid for aid in required if not state.has(aid, tick)]
    return {"allowed": not missing, "required": required, "missing": missing}
