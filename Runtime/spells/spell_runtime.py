from __future__ import annotations

from dataclasses import dataclass, field
from random import Random
from typing import Any, Dict, Iterable, List, Optional

from Runtime.formula_engine import FormulaTrace, clamp


@dataclass
class SpellEffect:
    effect_type: str
    magnitude: int = 0
    duration_ticks: int = 0
    school: str = "unknown"
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class StatusEffect:
    effect_id: str
    source_id: str
    target_id: str
    effect_type: str
    magnitude: int = 0
    remaining_ticks: int = 0
    provenance: str = "reforged_formula"
    metadata: Dict[str, Any] = field(default_factory=dict)

    @property
    def active(self) -> bool:
        return self.remaining_ticks != 0


@dataclass
class SpellCastRequest:
    caster: Dict[str, Any]
    target: Dict[str, Any]
    spell: Dict[str, Any]
    mode: str = "reforged_formula"
    seed: Optional[int] = None


@dataclass
class SpellCastResult:
    success: bool
    spell_id: str
    caster_id: str
    target_id: str
    cost_paid: Dict[str, int]
    hp_delta: int = 0
    mana_delta: int = 0
    cp_delta: int = 0
    status_effects: List[StatusEffect] = field(default_factory=list)
    traces: List[FormulaTrace] = field(default_factory=list)
    events: List[Dict[str, Any]] = field(default_factory=list)
    notes: List[str] = field(default_factory=list)


class StatusEffectRuntime:
    """Tracks ticking spell/status effects without claiming classic timing accuracy."""

    def __init__(self):
        self.effects: List[StatusEffect] = []

    def add(self, effect: StatusEffect) -> StatusEffect:
        self.effects.append(effect)
        return effect

    def active_for_target(self, target_id: str) -> List[StatusEffect]:
        return [effect for effect in self.effects if effect.target_id == target_id and effect.active]

    def tick(self) -> List[Dict[str, Any]]:
        events: List[Dict[str, Any]] = []
        for effect in self.effects:
            if effect.remaining_ticks > 0:
                effect.remaining_ticks -= 1
                events.append({
                    "event_type": "status_tick",
                    "effect_id": effect.effect_id,
                    "target_id": effect.target_id,
                    "effect_type": effect.effect_type,
                    "remaining_ticks": effect.remaining_ticks,
                    "magnitude": effect.magnitude,
                })
        return events

    def purge_expired(self) -> int:
        before = len(self.effects)
        self.effects = [effect for effect in self.effects if effect.active]
        return before - len(self.effects)


def _first_value(record: Dict[str, Any], keys: Iterable[str], default: Any = None) -> Any:
    for key in keys:
        if key in record and record[key] not in (None, ""):
            return record[key]
    return default


def _int_value(record: Dict[str, Any], keys: Iterable[str], default: int = 0) -> int:
    value = _first_value(record, keys, default)
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return default


class SpellRuntime:
    """Executable spell pipeline with explicit formula provenance.

    The runtime supports deterministic Reforged formulas now. Exact MajorMUD
    classic spell formulas remain UNKNOWN until evidence-backed.
    """

    def __init__(self, seed: Optional[int] = None):
        self.random = Random(seed)

    def mana_cost(self, spell: Dict[str, Any]) -> int:
        return max(0, _int_value(spell, ["mana_cost", "ManaCost", "mana", "cost", "Cost"], 0))

    def cp_cost(self, spell: Dict[str, Any]) -> int:
        return max(0, _int_value(spell, ["cp_cost", "CPCost", "cp", "CP"], 0))

    def parse_effects(self, spell: Dict[str, Any]) -> List[SpellEffect]:
        raw_type = str(_first_value(spell, ["effect_type", "EffectType", "type", "Type", "school", "School"], "unknown")).lower()
        name = str(_first_value(spell, ["name", "Name", "spell_name", "SpellName"], "")).lower()
        magnitude = _int_value(spell, ["magnitude", "Magnitude", "power", "Power", "damage", "Damage", "heal", "Heal"], 0)
        duration = _int_value(spell, ["duration", "Duration", "duration_ticks", "DurationTicks"], 0)

        if "heal" in raw_type or "heal" in name or "cure" in name:
            effect_type = "heal"
            if magnitude == 0:
                magnitude = 10
        elif "poison" in raw_type or "poison" in name:
            effect_type = "poison"
            if duration == 0:
                duration = 5
            if magnitude == 0:
                magnitude = 1
        elif any(token in raw_type or token in name for token in ["buff", "bless", "shield", "armor"]):
            effect_type = "buff"
            if duration == 0:
                duration = 10
            if magnitude == 0:
                magnitude = 1
        elif any(token in raw_type or token in name for token in ["blind", "stun", "fear", "slow", "debuff", "curse"]):
            effect_type = "debuff"
            if duration == 0:
                duration = 5
            if magnitude == 0:
                magnitude = 1
        elif any(token in raw_type or token in name for token in ["damage", "fire", "cold", "ice", "bolt", "blast", "harm"]):
            effect_type = "damage"
            if magnitude == 0:
                magnitude = 8
        else:
            effect_type = "unknown"
        return [SpellEffect(effect_type=effect_type, magnitude=magnitude, duration_ticks=duration, school=raw_type, metadata={"source": "parsed_or_defaulted"})]

    def spell_success_trace(self, caster: Dict[str, Any], target: Dict[str, Any], spell: Dict[str, Any], mode: str) -> FormulaTrace:
        trace = FormulaTrace("reforged_spell_success_v1", mode, {"caster": caster, "target": target, "spell": spell})
        cast_score = (
            _int_value(caster, ["casting_skill", "skill", "level"], 1)
            + _int_value(caster, ["intelligence", "willpower", "accuracy_stat"], 0)
            + _int_value(caster, ["level"], 1) * 2
            + _int_value(caster, ["focus_bonus", "spell_accuracy_bonus"], 0)
        )
        resist_score = (
            _int_value(target, ["resist", "magic_resist", "defense"], 0)
            + _int_value(target, ["willpower", "agility"], 0)
            + _int_value(target, ["level"], 1) * 2
            + _int_value(target, ["spell_defense_bonus", "resistance_bonus"], 0)
        )
        raw = 60 + ((cast_score - resist_score) * 2)
        chance = int(clamp(raw, 5, 95))
        trace.add_step("cast_score", cast_score)
        trace.add_step("resist_score", resist_score)
        trace.add_step("raw_success_chance", raw)
        trace.add_step("clamped_success_chance", chance)
        trace.result = chance
        return trace

    def resolve_cast(self, request: SpellCastRequest) -> SpellCastResult:
        caster = request.caster or {}
        target = request.target or {}
        spell = request.spell or {}
        rng = Random(request.seed) if request.seed is not None else self.random
        spell_id = str(_first_value(spell, ["runtime_id", "id", "spell_id", "SpellID", "name", "Name"], "unknown_spell"))
        caster_id = str(_first_value(caster, ["runtime_id", "id", "name"], "caster"))
        target_id = str(_first_value(target, ["runtime_id", "id", "name"], "target"))
        result = SpellCastResult(
            success=False,
            spell_id=spell_id,
            caster_id=caster_id,
            target_id=target_id,
            cost_paid={"mana": self.mana_cost(spell), "cp": self.cp_cost(spell)},
            notes=["Classic spell formulas are UNKNOWN; this cast uses explicit Reforged formula provenance."],
        )
        trace = self.spell_success_trace(caster, target, spell, request.mode)
        roll = rng.randint(1, 100)
        trace.rolls.append({"name": "spell_success_roll", "min": 1, "max": 100, "value": roll})
        result.traces.append(trace)
        result.events.append({"event_type": "spell_cast_started", "spell_id": spell_id, "caster_id": caster_id, "target_id": target_id})
        if roll > int(trace.result):
            result.events.append({"event_type": "spell_resisted_or_failed", "roll": roll, "chance": trace.result})
            return result

        result.success = True
        for effect in self.parse_effects(spell):
            if effect.effect_type == "damage":
                amount = max(1, effect.magnitude)
                result.hp_delta -= amount
                result.events.append({"event_type": "spell_damage", "target_id": target_id, "amount": amount})
            elif effect.effect_type == "heal":
                amount = max(1, effect.magnitude)
                result.hp_delta += amount
                result.events.append({"event_type": "spell_heal", "target_id": target_id, "amount": amount})
            elif effect.effect_type in {"buff", "debuff", "poison"}:
                status = StatusEffect(
                    effect_id=f"{spell_id}:{effect.effect_type}",
                    source_id=caster_id,
                    target_id=target_id,
                    effect_type=effect.effect_type,
                    magnitude=effect.magnitude,
                    remaining_ticks=effect.duration_ticks,
                    provenance=request.mode,
                    metadata={"spell_id": spell_id, **effect.metadata},
                )
                result.status_effects.append(status)
                result.events.append({"event_type": "status_applied", "effect_type": effect.effect_type, "target_id": target_id, "duration_ticks": effect.duration_ticks})
            else:
                result.events.append({"event_type": "spell_effect_unknown", "spell_id": spell_id})
        result.events.append({"event_type": "spell_cast_resolved", "success": result.success, "hp_delta": result.hp_delta})
        return result
