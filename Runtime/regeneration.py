"""HP/Mana/CP and regeneration runtime foundation for Runtime-0005.

This module provides deterministic, traceable Reforged formula helpers for
offline simulation. It does not claim exact classic MajorMUD formula parity.
Classic-derived formula promotion remains evidence-gated.
"""
from __future__ import annotations

from dataclasses import dataclass, asdict, field
from typing import Any, Dict, List, Optional

from Runtime.formula_engine import FormulaTrace, clamp


@dataclass
class VitalPoolResult:
    max_hp: int
    max_mana: int
    max_cp: int
    current_hp: int
    current_mana: int
    current_cp: int
    mode: str = "reforged_formula"
    classic_accuracy: str = "not_classic_claimed"
    notes: List[str] = field(default_factory=list)
    trace: Dict[str, Any] = field(default_factory=dict)

    def to_jsonable(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class RegenerationTickResult:
    hp_before: int
    hp_after: int
    hp_gained: int
    mana_before: int
    mana_after: int
    mana_gained: int
    cp_before: int
    cp_after: int
    cp_gained: int
    mode: str = "reforged_formula"
    classic_accuracy: str = "not_classic_claimed"
    notes: List[str] = field(default_factory=list)
    trace: Dict[str, Any] = field(default_factory=dict)

    def to_jsonable(self) -> Dict[str, Any]:
        return asdict(self)


def _stat(stats: Dict[str, Any], key: str, default: int = 50) -> int:
    try:
        return int(stats.get(key, default))
    except Exception:
        return default


def _bonus_from_stat(value: int, divisor: int = 10) -> int:
    """Small Reforged tuning helper: 50 is neutral; above/below shifts by divisor."""
    return int((int(value) - 50) // divisor)


def _find_record(records: List[Dict[str, Any]], key: Optional[str]) -> Optional[Dict[str, Any]]:
    if not key:
        return None
    norm = str(key).strip().lower().replace("_", "-")
    for rec in records:
        for cand in (rec.get("id"), rec.get("name"), rec.get("legacy_number")):
            if cand is not None and str(cand).strip().lower().replace("_", "-") == norm:
                return rec
    return None


def _record_int(record: Optional[Dict[str, Any]], names: List[str], default: int) -> int:
    if not isinstance(record, dict):
        return default
    for name in names:
        value = record.get(name)
        if isinstance(value, int):
            return value
        try:
            if value not in (None, ""):
                return int(value)
        except Exception:
            pass
    return default


def calculate_vital_pools(
    snapshot: Any,
    classes: Optional[List[Dict[str, Any]]] = None,
    races: Optional[List[Dict[str, Any]]] = None,
    equipment_totals: Optional[Dict[str, int]] = None,
    cp_awarded_from_levels: Optional[int] = None,
) -> VitalPoolResult:
    """Calculate max/current HP, mana, and CP for an offline snapshot.

    Reforged v1 design intent:
    - HP leans on level and HEA.
    - Mana leans on level and INT/WIL.
    - CP leans on explicit leveling CP plus level fallback.
    - Equipment totals can add hp/mana/cp/max_hp/max_mana/max_cp when present.
    """
    classes = classes or []
    races = races or []
    equipment_totals = equipment_totals or {}
    level = max(1, int(getattr(snapshot, "level", 1) or 1))
    stats = getattr(snapshot, "base_stats", {}) or {}
    current = getattr(snapshot, "current", {}) or {}
    class_rec = _find_record(classes, getattr(snapshot, "class_id", None))
    race_rec = _find_record(races, getattr(snapshot, "race_id", None))

    class_hp_base = _record_int(class_rec, ["base_hp", "hp_base", "hitPointsBase"], 20)
    class_hp_per_level = _record_int(class_rec, ["hp_per_level", "hitPointsPerLevel", "hpPerLevel"], 6)
    class_mana_base = _record_int(class_rec, ["base_mana", "mana_base", "magicPointsBase"], 10)
    class_mana_per_level = _record_int(class_rec, ["mana_per_level", "magicPointsPerLevel", "manaPerLevel"], 3)
    race_hp_bonus = _record_int(race_rec, ["hp_bonus", "hitPointsBonus", "hpBonus"], 0)
    race_mana_bonus = _record_int(race_rec, ["mana_bonus", "magicPointsBonus", "manaBonus"], 0)

    hp_trace = FormulaTrace(
        "reforged_vital_pools_v1",
        "reforged_formula",
        {"level": level, "stats": stats, "class_id": getattr(snapshot, "class_id", None), "race_id": getattr(snapshot, "race_id", None)},
        classic_accuracy="not_classic_claimed",
    )
    hea_bonus = _bonus_from_stat(_stat(stats, "hea"), divisor=5)
    int_bonus = _bonus_from_stat(_stat(stats, "int"), divisor=8)
    wil_bonus = _bonus_from_stat(_stat(stats, "wil"), divisor=8)

    max_hp = class_hp_base + (class_hp_per_level * level) + (hea_bonus * level) + race_hp_bonus
    max_mana = class_mana_base + (class_mana_per_level * level) + ((int_bonus + wil_bonus) * max(1, level // 2)) + race_mana_bonus
    max_cp = int(cp_awarded_from_levels if cp_awarded_from_levels is not None else current.get("cp_awarded_from_levels", level * 5))

    max_hp += int(equipment_totals.get("max_hp", equipment_totals.get("hp", 0)) or 0)
    max_mana += int(equipment_totals.get("max_mana", equipment_totals.get("mana", 0)) or 0)
    max_cp += int(equipment_totals.get("max_cp", equipment_totals.get("cp", 0)) or 0)

    max_hp = max(1, int(max_hp))
    max_mana = max(0, int(max_mana))
    max_cp = max(0, int(max_cp))

    hp_trace.add_step("class_hp_base", class_hp_base)
    hp_trace.add_step("class_hp_per_level", class_hp_per_level)
    hp_trace.add_step("hea_bonus_per_level", hea_bonus)
    hp_trace.add_step("race_hp_bonus", race_hp_bonus)
    hp_trace.add_step("max_hp", max_hp)
    hp_trace.add_step("class_mana_base", class_mana_base)
    hp_trace.add_step("class_mana_per_level", class_mana_per_level)
    hp_trace.add_step("int_bonus", int_bonus)
    hp_trace.add_step("wil_bonus", wil_bonus)
    hp_trace.add_step("race_mana_bonus", race_mana_bonus)
    hp_trace.add_step("max_mana", max_mana)
    hp_trace.add_step("max_cp", max_cp)
    hp_trace.result = {"max_hp": max_hp, "max_mana": max_mana, "max_cp": max_cp}

    cur_hp = int(current.get("hp", max_hp) or 0)
    cur_mana = int(current.get("mana", max_mana) or 0)
    cur_cp = int(current.get("cp", max_cp) or 0)

    return VitalPoolResult(
        max_hp=max_hp,
        max_mana=max_mana,
        max_cp=max_cp,
        current_hp=int(clamp(cur_hp, 0, max_hp)),
        current_mana=int(clamp(cur_mana, 0, max_mana)),
        current_cp=int(clamp(cur_cp, 0, max_cp)),
        notes=[
            "Runtime-0005 vital pools use Reforged formula mode, not verified classic parity.",
            "Class/race fields are consumed when present; otherwise safe defaults are used.",
        ],
        trace={"vital_pools": hp_trace.__dict__},
    )


def calculate_regeneration_tick(
    snapshot: Any,
    pools: VitalPoolResult,
    resting: bool = False,
    in_combat: bool = False,
    poison_or_bleed: int = 0,
    equipment_totals: Optional[Dict[str, int]] = None,
) -> RegenerationTickResult:
    """Apply one deterministic Reforged regeneration tick to HP/Mana/CP."""
    equipment_totals = equipment_totals or {}
    stats = getattr(snapshot, "base_stats", {}) or {}
    level = max(1, int(getattr(snapshot, "level", 1) or 1))
    trace = FormulaTrace(
        "reforged_regeneration_tick_v1",
        "reforged_formula",
        {"level": level, "resting": resting, "in_combat": in_combat, "poison_or_bleed": poison_or_bleed},
        classic_accuracy="not_classic_claimed",
    )

    hea_bonus = max(0, _bonus_from_stat(_stat(stats, "hea"), divisor=10))
    wil_bonus = max(0, _bonus_from_stat(_stat(stats, "wil"), divisor=10))
    rest_mult = 2 if resting else 1
    combat_penalty = 0.5 if in_combat else 1.0

    hp_gain = int(max(0, round((1 + level // 5 + hea_bonus + int(equipment_totals.get("hp_regen", 0) or 0)) * rest_mult * combat_penalty)))
    mana_gain = int(max(0, round((1 + level // 6 + wil_bonus + int(equipment_totals.get("mana_regen", 0) or 0)) * rest_mult * combat_penalty)))
    cp_gain = int(max(0, round((int(equipment_totals.get("cp_regen", 0) or 0)) * rest_mult * combat_penalty)))

    hp_gain = max(0, hp_gain - max(0, int(poison_or_bleed or 0)))

    hp_after = int(clamp(pools.current_hp + hp_gain, 0, pools.max_hp))
    mana_after = int(clamp(pools.current_mana + mana_gain, 0, pools.max_mana))
    cp_after = int(clamp(pools.current_cp + cp_gain, 0, pools.max_cp))

    trace.add_step("hea_bonus", hea_bonus)
    trace.add_step("wil_bonus", wil_bonus)
    trace.add_step("rest_multiplier", rest_mult)
    trace.add_step("combat_penalty", combat_penalty)
    trace.add_step("hp_gain_pre_cap", hp_gain)
    trace.add_step("mana_gain_pre_cap", mana_gain)
    trace.add_step("cp_gain_pre_cap", cp_gain)
    trace.add_step("hp_after", hp_after)
    trace.add_step("mana_after", mana_after)
    trace.add_step("cp_after", cp_after)
    trace.result = {"hp_after": hp_after, "mana_after": mana_after, "cp_after": cp_after}

    return RegenerationTickResult(
        hp_before=pools.current_hp,
        hp_after=hp_after,
        hp_gained=hp_after - pools.current_hp,
        mana_before=pools.current_mana,
        mana_after=mana_after,
        mana_gained=mana_after - pools.current_mana,
        cp_before=pools.current_cp,
        cp_after=cp_after,
        cp_gained=cp_after - pools.current_cp,
        notes=["Runtime-0005 regen uses Reforged formula mode; classic tick timing/formula remains UNKNOWN."],
        trace={"regeneration_tick": trace.__dict__},
    )


def apply_vital_pools_to_snapshot(snapshot: Any, pools: VitalPoolResult) -> Any:
    snapshot.current["max_hp"] = pools.max_hp
    snapshot.current["max_mana"] = pools.max_mana
    snapshot.current["max_cp"] = pools.max_cp
    snapshot.current["hp"] = pools.current_hp
    snapshot.current["mana"] = pools.current_mana
    snapshot.current["cp"] = pools.current_cp
    snapshot.flags["vital_pool_mode"] = pools.mode
    snapshot.flags["vital_pool_classic_accuracy"] = pools.classic_accuracy
    return snapshot


def apply_regeneration_tick_to_snapshot(snapshot: Any, tick: RegenerationTickResult) -> Any:
    snapshot.current["hp"] = tick.hp_after
    snapshot.current["mana"] = tick.mana_after
    snapshot.current["cp"] = tick.cp_after
    snapshot.flags["regeneration_mode"] = tick.mode
    snapshot.flags["regeneration_classic_accuracy"] = tick.classic_accuracy
    return snapshot
