"""EXP and leveling runtime foundation for Runtime-0004.

This module provides deterministic, traceable progression helpers. It does not
claim exact classic MajorMUD formula parity. Values sourced from /Data are used
when present; missing mechanics remain explicit and evidence-gated.
"""
from __future__ import annotations

from dataclasses import dataclass, asdict, field
from typing import Any, Dict, List, Optional

from Runtime.formula_engine import FormulaTrace


@dataclass
class LevelingResult:
    current_level: int
    target_level: int
    current_exp: int
    exp_for_current_level: int
    exp_for_next_level: Optional[int]
    total_cp_awarded: int
    levels_gained: int
    mode: str = "reforged_formula"
    classic_accuracy: str = "not_classic_claimed"
    notes: List[str] = field(default_factory=list)
    trace: Dict[str, Any] = field(default_factory=dict)

    def to_jsonable(self) -> Dict[str, Any]:
        return asdict(self)


def _find_by_id_or_name(records: List[Dict[str, Any]], key: Optional[str]) -> Optional[Dict[str, Any]]:
    if not key:
        return None
    norm = str(key).strip().lower().replace("_", "-")
    for rec in records:
        candidates = [rec.get("id"), rec.get("name"), rec.get("legacy_number")]
        for cand in candidates:
            if cand is not None and str(cand).strip().lower().replace("_", "-") == norm:
                return rec
    return None


def class_exp_modifier(classes: List[Dict[str, Any]], class_id: Optional[str]) -> int:
    rec = _find_by_id_or_name(classes, class_id)
    if rec and isinstance(rec.get("exp_modifier"), int):
        return int(rec["exp_modifier"])
    return 100


def race_exp_modifier(races: List[Dict[str, Any]], race_id: Optional[str]) -> int:
    rec = _find_by_id_or_name(races, race_id)
    if rec and isinstance(rec.get("exp_modifier"), int):
        return int(rec["exp_modifier"])
    return 0


def cp_per_level(leveling_config: Dict[str, Any], level: int) -> int:
    base = int(leveling_config.get("pointsPerLevel", leveling_config.get("pointsPerLevelBase", 5)))
    every = int(leveling_config.get("pointsPerLevelBonusEvery", 0) or 0)
    bonus = int(leveling_config.get("pointsPerLevelBonusAmount", 0) or 0)
    if every > 0 and level > 0 and level % every == 0:
        return base + bonus
    return base


def reforged_exp_required_for_level(level: int, class_modifier: int = 100, race_modifier: int = 0) -> FormulaTrace:
    """Return total EXP required to reach `level` under the Reforged v1 curve.

    Design intent: simple, deterministic, easy to tune. Class modifiers are
    treated as percentages where 100 is neutral. Race modifiers are treated as
    additive percentage points from canonical race records when available.
    """
    trace = FormulaTrace(
        "reforged_exp_required_for_level_v1",
        "reforged_formula",
        {"level": level, "class_modifier": class_modifier, "race_modifier": race_modifier},
        classic_accuracy="not_classic_claimed",
    )
    if level <= 1:
        trace.add_step("base_case", 0, "Level 1 starts at 0 EXP.")
        trace.result = 0
        return trace
    base_curve = 1000 * ((level - 1) ** 2)
    modifier_percent = max(1, class_modifier + race_modifier)
    required = int(round(base_curve * (modifier_percent / 100.0)))
    trace.add_step("base_curve", base_curve, "1000 * (level - 1)^2")
    trace.add_step("modifier_percent", modifier_percent, "class exp modifier plus race exp modifier")
    trace.add_step("required_exp", required)
    trace.result = required
    return trace


def calculate_level_from_exp(
    current_exp: int,
    current_level: int,
    leveling_config: Dict[str, Any],
    classes: List[Dict[str, Any]],
    races: List[Dict[str, Any]],
    class_id: Optional[str] = None,
    race_id: Optional[str] = None,
) -> LevelingResult:
    max_level = int(leveling_config.get("maxLevel", 75) or 75)
    current_exp = max(0, int(current_exp))
    current_level = max(1, int(current_level))
    cls_mod = class_exp_modifier(classes, class_id)
    rce_mod = race_exp_modifier(races, race_id)
    notes: List[str] = [
        "Runtime-0004 progression uses Reforged formula mode, not verified classic parity.",
        "Class/race EXP modifiers are read from canonical /Data when available.",
    ]

    target = 1
    level_traces: List[Dict[str, Any]] = []
    for level in range(1, max_level + 1):
        trace = reforged_exp_required_for_level(level, cls_mod, rce_mod)
        level_traces.append({"level": level, "required": trace.result})
        if current_exp >= int(trace.result):
            target = level
        else:
            break

    exp_current_trace = reforged_exp_required_for_level(target, cls_mod, rce_mod)
    next_level = target + 1 if target < max_level else None
    exp_next = None
    if next_level is not None:
        exp_next = int(reforged_exp_required_for_level(next_level, cls_mod, rce_mod).result)

    levels_gained = max(0, target - current_level)
    cp_awarded = sum(cp_per_level(leveling_config, lvl) for lvl in range(current_level + 1, target + 1))

    return LevelingResult(
        current_level=current_level,
        target_level=target,
        current_exp=current_exp,
        exp_for_current_level=int(exp_current_trace.result),
        exp_for_next_level=exp_next,
        total_cp_awarded=cp_awarded,
        levels_gained=levels_gained,
        trace={
            "class_modifier": cls_mod,
            "race_modifier": rce_mod,
            "max_level": max_level,
            "checked_levels": level_traces[: max(5, min(len(level_traces), target + 2))],
        },
        notes=notes,
    )


def apply_leveling_to_snapshot(snapshot: Any, result: LevelingResult) -> Any:
    """Mutate a CharacterSnapshot-like object with leveling result data."""
    snapshot.level = result.target_level
    snapshot.current.setdefault("exp", result.current_exp)
    snapshot.current["exp"] = result.current_exp
    snapshot.current["cp_awarded_from_levels"] = result.total_cp_awarded
    snapshot.flags["leveling_mode"] = result.mode
    snapshot.flags["leveling_classic_accuracy"] = result.classic_accuracy
    return snapshot
