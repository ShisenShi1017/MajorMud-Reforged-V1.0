"""Character snapshot runtime for Runtime-0003.

This module creates deterministic offline character snapshots for formula and
simulator work. It does not claim classic formula accuracy. Classic-derived
values must be added later only with evidence.
"""
from __future__ import annotations

from dataclasses import dataclass, field, asdict
from typing import Any, Dict, List, Optional

DEFAULT_STATS = {
    "str": 50,
    "int": 50,
    "wil": 50,
    "agl": 50,
    "hea": 50,
    "cha": 50,
}

@dataclass
class SnapshotIssue:
    severity: str
    code: str
    message: str

@dataclass
class CharacterSnapshot:
    runtime_id: str
    name: str
    race_id: Optional[str]
    class_id: Optional[str]
    level: int
    base_stats: Dict[str, int]
    current: Dict[str, int] = field(default_factory=dict)
    skills: Dict[str, int] = field(default_factory=dict)
    equipment: Dict[str, Optional[str]] = field(default_factory=dict)
    inventory: List[str] = field(default_factory=list)
    flags: Dict[str, Any] = field(default_factory=dict)
    provenance: Dict[str, Any] = field(default_factory=dict)
    issues: List[SnapshotIssue] = field(default_factory=list)

    def to_jsonable(self) -> Dict[str, Any]:
        return asdict(self)

    def equipped_item_ids(self) -> List[str]:
        return [v for v in self.equipment.values() if isinstance(v, str) and v]


def _coerce_stats(stats: Optional[Dict[str, Any]]) -> Dict[str, int]:
    out = dict(DEFAULT_STATS)
    if isinstance(stats, dict):
        for key in DEFAULT_STATS:
            try:
                out[key] = int(stats.get(key, out[key]))
            except Exception:
                out[key] = DEFAULT_STATS[key]
    return out


def create_character_snapshot(
    name: str = "Runtime Test Character",
    race_id: Optional[str] = None,
    class_id: Optional[str] = None,
    level: int = 1,
    base_stats: Optional[Dict[str, Any]] = None,
    equipment: Optional[Dict[str, Optional[str]]] = None,
    skills: Optional[Dict[str, int]] = None,
    inventory: Optional[List[str]] = None,
) -> CharacterSnapshot:
    """Create an offline character snapshot suitable for validators/simulators."""
    issues: List[SnapshotIssue] = []
    if level < 1:
        issues.append(SnapshotIssue("warning", "LEVEL_CLAMPED", f"Level {level} clamped to 1."))
        level = 1
    stats = _coerce_stats(base_stats)
    snapshot = CharacterSnapshot(
        runtime_id="character_" + "".join(ch.lower() if ch.isalnum() else "_" for ch in name).strip("_"),
        name=name,
        race_id=race_id,
        class_id=class_id,
        level=int(level),
        base_stats=stats,
        current={"hp": 0, "mana": 0, "cp": 0},
        skills=skills or {},
        equipment=equipment or {},
        inventory=inventory or [],
        flags={},
        provenance={
            "mode": "runtime_snapshot",
            "classic_accuracy": "not_classic_claimed",
            "note": "Offline snapshot shell; exact classic derived formulas remain evidence-gated.",
        },
        issues=issues,
    )
    return snapshot
