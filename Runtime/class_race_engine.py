from __future__ import annotations

import json
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

CANONICAL_STATS = ("str", "int", "wil", "agl", "hea", "cha")

@dataclass(frozen=True)
class RuntimeClass:
    id: str
    name: str
    description: str = ""
    hp_per_level: int = 0
    mana_per_level: int = 0
    stat_bonuses: Dict[str, int] = field(default_factory=dict)
    secondary_bonuses: Dict[str, int] = field(default_factory=dict)
    equipment_rules: Dict[str, Any] = field(default_factory=dict)
    source: Any = None
    provenance: str = "canonical_data"
    formula_mode: str = "data_backed_no_classic_formula_claim"

@dataclass(frozen=True)
class RuntimeRace:
    id: str
    legacy_number: Optional[int]
    name: str
    min_stats: Dict[str, int]
    max_stats: Dict[str, int]
    hp_bonus: int = 0
    cp_cost: int = 100
    exp_modifier: int = 0
    abilities: List[Dict[str, Any]] = field(default_factory=list)
    source: Any = None
    provenance: str = "canonical_data"
    formula_mode: str = "data_backed_no_classic_formula_claim"

@dataclass(frozen=True)
class ClassRaceProfile:
    character_name: str
    class_id: str
    race_id: str
    level: int
    stats: Dict[str, int]
    hp_per_level: int
    mana_per_level: int
    race_hp_bonus: int
    race_cp_cost: int
    race_exp_modifier: int
    class_stat_bonuses: Dict[str, int]
    race_abilities: List[Dict[str, Any]]
    validation_warnings: List[str]
    trace: List[Dict[str, Any]]

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)

class ClassRaceEngine:
    """Evidence-preserving class/race runtime adapter.

    This module intentionally does not invent hidden MajorMUD class/race formulas.
    It normalizes canonical Data/classes.json and Data/races.json into runtime-safe
    objects, validates requested character stats against race min/max ranges, and
    returns a trace that downstream calculators can consume.
    """

    def __init__(self, data_dir: str | Path):
        self.data_dir = Path(data_dir)
        self.classes: Dict[str, RuntimeClass] = {}
        self.races: Dict[str, RuntimeRace] = {}
        self.load()

    @staticmethod
    def _slug(value: str) -> str:
        return str(value).strip().lower().replace(" ", "_").replace("-", "_")

    def load(self) -> None:
        classes_path = self.data_dir / "classes.json"
        races_path = self.data_dir / "races.json"
        if not classes_path.exists():
            raise FileNotFoundError(f"Missing classes data: {classes_path}")
        if not races_path.exists():
            raise FileNotFoundError(f"Missing races data: {races_path}")

        raw_classes = json.loads(classes_path.read_text(encoding="utf-8"))
        raw_races = json.loads(races_path.read_text(encoding="utf-8"))
        if not isinstance(raw_classes, list):
            raise ValueError("classes.json must contain a list")
        if not isinstance(raw_races, list):
            raise ValueError("races.json must contain a list")

        self.classes = {}
        for row in raw_classes:
            class_id = row.get("id") or self._slug(row.get("name", "unknown_class"))
            self.classes[class_id] = RuntimeClass(
                id=class_id,
                name=row.get("name", class_id),
                description=row.get("description", ""),
                hp_per_level=int(row.get("hpPerLevel", row.get("hp_per_level", 0)) or 0),
                mana_per_level=int(row.get("manaPerLevel", row.get("mana_per_level", 0)) or 0),
                stat_bonuses=dict(row.get("statBonuses", row.get("stat_bonuses", {})) or {}),
                secondary_bonuses=dict(row.get("secondaryBonuses", row.get("secondary_bonuses", {})) or {}),
                equipment_rules=dict(row.get("equipmentRules", row.get("equipment_rules", {})) or {}),
                source=row.get("source"),
            )

        self.races = {}
        for row in raw_races:
            race_id = row.get("id") or self._slug(row.get("name", "unknown_race"))
            self.races[race_id] = RuntimeRace(
                id=race_id,
                legacy_number=row.get("legacy_number"),
                name=row.get("name", race_id),
                min_stats=dict(row.get("min_stats", {}) or {}),
                max_stats=dict(row.get("max_stats", {}) or {}),
                hp_bonus=int(row.get("hp_bonus", 0) or 0),
                cp_cost=int(row.get("cp_cost", 100) or 100),
                exp_modifier=int(row.get("exp_modifier", 0) or 0),
                abilities=list(row.get("abilities", []) or []),
                source=row.get("source"),
            )

    def list_classes(self) -> List[RuntimeClass]:
        return sorted(self.classes.values(), key=lambda c: c.name.lower())

    def list_races(self) -> List[RuntimeRace]:
        return sorted(self.races.values(), key=lambda r: r.name.lower())

    def get_class(self, class_id_or_name: str) -> RuntimeClass:
        key = self._slug(class_id_or_name)
        if key in self.classes:
            return self.classes[key]
        for klass in self.classes.values():
            if self._slug(klass.name) == key:
                return klass
        raise KeyError(f"Unknown class: {class_id_or_name}")

    def get_race(self, race_id_or_name: str) -> RuntimeRace:
        key = self._slug(race_id_or_name)
        if key in self.races:
            return self.races[key]
        for race in self.races.values():
            if self._slug(race.name) == key:
                return race
        raise KeyError(f"Unknown race: {race_id_or_name}")

    def validate_race_stats(self, race: RuntimeRace, stats: Dict[str, int]) -> Tuple[bool, List[str]]:
        warnings: List[str] = []
        ok = True
        for stat in CANONICAL_STATS:
            if stat not in stats:
                ok = False
                warnings.append(f"Missing stat: {stat}")
                continue
            value = int(stats[stat])
            low = race.min_stats.get(stat)
            high = race.max_stats.get(stat)
            if low is not None and value < int(low):
                ok = False
                warnings.append(f"{stat}={value} below {race.name} minimum {low}")
            if high is not None and value > int(high):
                ok = False
                warnings.append(f"{stat}={value} above {race.name} maximum {high}")
        return ok, warnings

    def clamp_stats_to_race(self, race: RuntimeRace, stats: Dict[str, int]) -> Dict[str, int]:
        clamped = {}
        for stat in CANONICAL_STATS:
            value = int(stats.get(stat, race.min_stats.get(stat, 0)))
            low = race.min_stats.get(stat)
            high = race.max_stats.get(stat)
            if low is not None:
                value = max(value, int(low))
            if high is not None:
                value = min(value, int(high))
            clamped[stat] = value
        return clamped

    def build_profile(
        self,
        character_name: str,
        class_id: str,
        race_id: str,
        level: int,
        stats: Optional[Dict[str, int]] = None,
        clamp_invalid_stats: bool = False,
    ) -> ClassRaceProfile:
        klass = self.get_class(class_id)
        race = self.get_race(race_id)
        level = max(1, int(level))
        if stats is None:
            stats = {stat: int(race.min_stats.get(stat, 0)) for stat in CANONICAL_STATS}
        stats = {stat: int(stats.get(stat, 0)) for stat in CANONICAL_STATS}
        ok, warnings = self.validate_race_stats(race, stats)
        if not ok and clamp_invalid_stats:
            before = dict(stats)
            stats = self.clamp_stats_to_race(race, stats)
            _, warnings = self.validate_race_stats(race, stats)
            warnings.insert(0, f"Stats clamped to race bounds from {before} to {stats}")

        trace = [
            {"step": "load_class", "class_id": klass.id, "name": klass.name, "source": klass.source},
            {"step": "load_race", "race_id": race.id, "name": race.name, "source": race.source},
            {"step": "race_stat_bounds", "min": race.min_stats, "max": race.max_stats},
            {"step": "formula_policy", "mode": "data_backed_no_classic_formula_claim", "note": "No hidden class/race formulas were invented."},
        ]

        return ClassRaceProfile(
            character_name=character_name,
            class_id=klass.id,
            race_id=race.id,
            level=level,
            stats=stats,
            hp_per_level=klass.hp_per_level,
            mana_per_level=klass.mana_per_level,
            race_hp_bonus=race.hp_bonus,
            race_cp_cost=race.cp_cost,
            race_exp_modifier=race.exp_modifier,
            class_stat_bonuses=klass.stat_bonuses,
            race_abilities=race.abilities,
            validation_warnings=warnings,
            trace=trace,
        )

    def summarize(self) -> Dict[str, Any]:
        return {
            "class_count": len(self.classes),
            "race_count": len(self.races),
            "classes": [asdict(c) for c in self.list_classes()],
            "races": [asdict(r) for r in self.list_races()],
            "formula_policy": "Data-backed adapter only; no unverified classic formulas promoted.",
        }
