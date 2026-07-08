"""MajorMUD Reforged Runtime-0013C Quest Engine.

Quest progress is intentionally modeled around ability flags because user
research indicates classic quest flags behave as ability flags. This module
implements generic quest definitions/progress/reward hooks without inventing
classic quest formulas or hidden objective logic.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any, Dict, Iterable, List, Optional

try:
    from Runtime.abilities.ability_engine import AbilityGrant, AbilityState
except Exception:  # pragma: no cover
    from ..abilities.ability_engine import AbilityGrant, AbilityState


@dataclass(frozen=True)
class QuestObjective:
    objective_id: str
    kind: str
    target: str = ""
    required_count: int = 1
    required_abilities: List[str] = field(default_factory=list)

    @staticmethod
    def from_record(record: Dict[str, Any]) -> "QuestObjective":
        return QuestObjective(
            objective_id=str(record.get("id") or record.get("objective_id") or record.get("name") or "objective"),
            kind=str(record.get("kind") or record.get("type") or "unknown"),
            target=str(record.get("target") or record.get("target_id") or ""),
            required_count=int(record.get("required_count") or record.get("count") or 1),
            required_abilities=list(record.get("required_abilities") or record.get("ability_flags") or []),
        )


@dataclass(frozen=True)
class QuestDefinition:
    quest_id: str
    name: str
    description: str = ""
    required_abilities: List[str] = field(default_factory=list)
    reward_abilities: List[str] = field(default_factory=list)
    objectives: List[QuestObjective] = field(default_factory=list)
    source: Dict[str, Any] = field(default_factory=dict)
    implementation_status: str = "unknown"

    @staticmethod
    def from_record(record: Dict[str, Any]) -> "QuestDefinition":
        objectives = [QuestObjective.from_record(o) for o in record.get("objectives", [])]
        return QuestDefinition(
            quest_id=str(record.get("id") or record.get("quest_id") or record.get("name") or "quest"),
            name=str(record.get("name") or record.get("id") or "Unnamed Quest"),
            description=str(record.get("description") or ""),
            required_abilities=list(record.get("required_abilities") or record.get("requiredAbilityFlags") or []),
            reward_abilities=list(record.get("reward_abilities") or record.get("rewardAbilityFlags") or []),
            objectives=objectives,
            source=record.get("source") or {},
            implementation_status=str(record.get("implementationStatus") or "unknown"),
        )


@dataclass
class QuestProgress:
    quest_id: str
    state: str = "not_started"  # not_started, active, ready, completed, failed
    objective_counts: Dict[str, int] = field(default_factory=dict)
    started_at: Optional[str] = None
    completed_at: Optional[str] = None
    trace: List[Dict[str, Any]] = field(default_factory=list)

    def serialize(self) -> Dict[str, Any]:
        return self.__dict__.copy()


class QuestCatalog:
    def __init__(self, definitions: Iterable[QuestDefinition]):
        self.by_id: Dict[str, QuestDefinition] = {q.quest_id: q for q in definitions}
        self.by_name: Dict[str, QuestDefinition] = {q.name.lower(): q for q in definitions}

    @classmethod
    def from_records(cls, records: Iterable[Dict[str, Any]]) -> "QuestCatalog":
        return cls(QuestDefinition.from_record(r) for r in records)

    def resolve(self, quest_ref: Any) -> Optional[QuestDefinition]:
        key = str(quest_ref)
        return self.by_id.get(key) or self.by_name.get(key.lower())


class QuestState:
    def __init__(self) -> None:
        self.progress: Dict[str, QuestProgress] = {}
        self.trace: List[Dict[str, Any]] = []

    def start(self, quest: QuestDefinition, abilities: AbilityState) -> Dict[str, Any]:
        missing = [aid for aid in quest.required_abilities if not abilities.has(aid)]
        if missing:
            event = {"event": "quest_start_blocked", "quest_id": quest.quest_id, "missing_abilities": missing}
            self.trace.append(event)
            return event
        prog = self.progress.setdefault(quest.quest_id, QuestProgress(quest_id=quest.quest_id))
        prog.state = "active"
        prog.started_at = prog.started_at or datetime.now(timezone.utc).isoformat()
        event = {"event": "quest_started", "quest_id": quest.quest_id, "classicAccuracy": "ability_flag_state_tracking"}
        prog.trace.append(event); self.trace.append(event)
        return event

    def record_objective(self, quest: QuestDefinition, objective_id: str, amount: int = 1) -> Dict[str, Any]:
        prog = self.progress.setdefault(quest.quest_id, QuestProgress(quest_id=quest.quest_id, state="active"))
        prog.objective_counts[objective_id] = prog.objective_counts.get(objective_id, 0) + amount
        ready = self._is_ready(quest, prog)
        if ready and prog.state == "active":
            prog.state = "ready"
        event = {"event": "quest_objective_progress", "quest_id": quest.quest_id, "objective_id": objective_id, "count": prog.objective_counts[objective_id], "ready": ready}
        prog.trace.append(event); self.trace.append(event)
        return event

    def complete(self, quest: QuestDefinition, abilities: AbilityState) -> Dict[str, Any]:
        prog = self.progress.setdefault(quest.quest_id, QuestProgress(quest_id=quest.quest_id))
        if not self._is_ready(quest, prog):
            event = {"event": "quest_complete_blocked", "quest_id": quest.quest_id, "reason": "objectives_not_ready"}
            self.trace.append(event)
            return event
        prog.state = "completed"
        prog.completed_at = datetime.now(timezone.utc).isoformat()
        grants = []
        for aid in quest.reward_abilities:
            grant = AbilityGrant(ability_id=aid, source_type="quest", source_id=quest.quest_id, scope="quest")
            grants.append(abilities.grant(grant))
        event = {"event": "quest_completed", "quest_id": quest.quest_id, "reward_ability_count": len(grants)}
        prog.trace.append(event); self.trace.append(event)
        return event

    def _is_ready(self, quest: QuestDefinition, prog: QuestProgress) -> bool:
        if not quest.objectives:
            return prog.state in {"active", "ready"}
        for obj in quest.objectives:
            if prog.objective_counts.get(obj.objective_id, 0) < obj.required_count:
                return False
        return True

    def serialize(self) -> Dict[str, Any]:
        return {"progress": {qid: p.serialize() for qid, p in self.progress.items()}, "trace": self.trace}
