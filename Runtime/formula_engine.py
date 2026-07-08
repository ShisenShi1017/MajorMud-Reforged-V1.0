from __future__ import annotations

from dataclasses import dataclass, field
from random import Random
from typing import Any, Callable, Dict, List, Optional

@dataclass
class FormulaTrace:
    formula_id: str
    mode: str
    inputs: Dict[str, Any]
    steps: List[Dict[str, Any]] = field(default_factory=list)
    rolls: List[Dict[str, Any]] = field(default_factory=list)
    result: Any = None
    classic_accuracy: str = "not_classic_claimed"

    def add_step(self, name: str, value: Any, note: str = "") -> None:
        self.steps.append({"name": name, "value": value, "note": note})

class FormulaEngine:
    def __init__(self, seed: Optional[int] = None):
        self.random = Random(seed)
        self.formulas: Dict[str, Callable[..., FormulaTrace]] = {}

    def register(self, formula_id: str, func: Callable[..., FormulaTrace]) -> None:
        self.formulas[formula_id] = func

    def run(self, formula_id: str, **kwargs: Any) -> FormulaTrace:
        if formula_id not in self.formulas:
            raise KeyError(f"Formula not registered: {formula_id}")
        return self.formulas[formula_id](**kwargs)

def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))

def reforged_hit_chance_v1(
    attacker: Dict[str, Any],
    defender: Dict[str, Any],
    mode: str = "reforged_formula",
) -> FormulaTrace:
    trace = FormulaTrace("reforged_hit_chance_v1", mode, {"attacker": attacker, "defender": defender})
    attack_score = (
        attacker.get("weapon_skill", 0)
        + attacker.get("accuracy_stat", 0)
        + attacker.get("level", 1) * 2
        + attacker.get("item_accuracy_bonus", 0)
        + attacker.get("spell_accuracy_bonus", 0)
    )
    defense_score = (
        defender.get("defense", 0)
        + defender.get("agility", 0)
        + defender.get("level", 1) * 2
        + defender.get("armor_defense_bonus", 0)
        + defender.get("spell_defense_bonus", 0)
    )
    raw = 50 + ((attack_score - defense_score) * 2)
    result = clamp(raw, 5, 95)
    trace.add_step("attack_score", attack_score)
    trace.add_step("defense_score", defense_score)
    trace.add_step("raw_hit_chance", raw)
    trace.add_step("clamped_hit_chance", result)
    trace.result = result
    return trace

def reforged_damage_v1(attacker: Dict[str, Any], defender: Dict[str, Any], weapon_roll: int, mode: str = "reforged_formula") -> FormulaTrace:
    trace = FormulaTrace("reforged_damage_v1", mode, {"attacker": attacker, "defender": defender, "weapon_roll": weapon_roll})
    damage = (
        weapon_roll
        + attacker.get("strength_bonus", 0)
        + attacker.get("weapon_bonus", 0)
        + attacker.get("skill_bonus", 0)
        + attacker.get("class_bonus", 0)
        - defender.get("damage_reduction", 0)
    )
    result = max(1, damage)
    trace.add_step("weapon_roll", weapon_roll)
    trace.add_step("pre_minimum_damage", damage)
    trace.add_step("final_damage", result)
    trace.result = result
    return trace

def default_formula_engine(seed: Optional[int] = None) -> FormulaEngine:
    engine = FormulaEngine(seed=seed)
    engine.register("reforged_hit_chance_v1", reforged_hit_chance_v1)
    engine.register("reforged_damage_v1", reforged_damage_v1)
    return engine
