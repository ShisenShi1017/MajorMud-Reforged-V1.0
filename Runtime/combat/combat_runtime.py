"""Executable combat runtime foundation for Runtime-0009.

This module intentionally separates executable Reforged formulas from Classic claims.
It provides deterministic combat simulation, traces every calculation, and keeps the
formula mode explicit on every event.
"""
from __future__ import annotations

from dataclasses import dataclass, field, asdict
from random import Random
from typing import Any, Dict, List, Optional, Tuple

from Runtime.formula_engine import default_formula_engine, FormulaTrace


@dataclass
class CombatantSnapshot:
    runtime_id: str
    name: str
    kind: str  # player, monster, npc
    level: int = 1
    max_hp: int = 10
    hp: int = 10
    stats: Dict[str, int] = field(default_factory=dict)
    skills: Dict[str, int] = field(default_factory=dict)
    equipment_totals: Dict[str, int] = field(default_factory=dict)
    flags: Dict[str, Any] = field(default_factory=dict)
    provenance: Dict[str, Any] = field(default_factory=lambda: {
        "classic_accuracy": "not_classic_claimed",
        "mode": "reforged_formula",
    })

    @property
    def alive(self) -> bool:
        return self.hp > 0

    def to_formula_attacker(self) -> Dict[str, Any]:
        weapon_skill = max(self.skills.values()) if self.skills else 0
        return {
            "weapon_skill": weapon_skill,
            "accuracy_stat": self.stats.get("agl", self.stats.get("dex", 50)) // 5,
            "level": self.level,
            "item_accuracy_bonus": self.equipment_totals.get("accuracy_bonus", 0),
            "spell_accuracy_bonus": self.flags.get("spell_accuracy_bonus", 0),
            "strength_bonus": max(0, (self.stats.get("str", 50) - 50) // 10),
            "weapon_bonus": self.equipment_totals.get("weapon_bonus", 0),
            "skill_bonus": weapon_skill // 20,
            "class_bonus": self.flags.get("class_damage_bonus", 0),
        }

    def to_formula_defender(self) -> Dict[str, Any]:
        return {
            "defense": self.equipment_totals.get("armor_class_bonus", 0),
            "agility": self.stats.get("agl", self.stats.get("dex", 50)) // 5,
            "level": self.level,
            "armor_defense_bonus": self.equipment_totals.get("armor_class_bonus", 0),
            "spell_defense_bonus": self.flags.get("spell_defense_bonus", 0),
            "damage_reduction": self.equipment_totals.get("damage_resistance_bonus", 0),
        }

    def weapon_damage_range(self) -> Tuple[int, int]:
        low = int(self.equipment_totals.get("min_hit", self.flags.get("natural_min_hit", 1)))
        high = int(self.equipment_totals.get("max_hit", self.flags.get("natural_max_hit", max(1, low + 2))))
        if high < low:
            high = low
        return max(1, low), max(1, high)


@dataclass
class CombatEvent:
    round_number: int
    event_type: str
    actor_id: str
    target_id: Optional[str]
    message: str
    data: Dict[str, Any] = field(default_factory=dict)


@dataclass
class AttackResult:
    attacker_id: str
    defender_id: str
    hit: bool
    attack_roll: int
    hit_chance: float
    weapon_roll: int = 0
    damage: int = 0
    defender_hp_after: int = 0
    traces: List[FormulaTrace] = field(default_factory=list)
    classic_accuracy: str = "not_classic_claimed"

    def to_jsonable(self) -> Dict[str, Any]:
        data = asdict(self)
        data["traces"] = [asdict(t) for t in self.traces]
        return data


@dataclass
class CombatState:
    combatants: Dict[str, CombatantSnapshot]
    round_number: int = 0
    events: List[CombatEvent] = field(default_factory=list)
    mode: str = "reforged_formula"
    classic_accuracy: str = "not_classic_claimed"

    def living_by_kind(self, kind: str) -> List[CombatantSnapshot]:
        return [c for c in self.combatants.values() if c.kind == kind and c.alive]

    def living_enemies_of(self, actor: CombatantSnapshot) -> List[CombatantSnapshot]:
        return [c for c in self.combatants.values() if c.alive and c.runtime_id != actor.runtime_id and c.kind != actor.kind]

    def add_event(self, event_type: str, actor_id: str, target_id: Optional[str], message: str, data: Optional[Dict[str, Any]] = None) -> None:
        self.events.append(CombatEvent(self.round_number, event_type, actor_id, target_id, message, data or {}))

    def to_jsonable(self) -> Dict[str, Any]:
        return {
            "round_number": self.round_number,
            "mode": self.mode,
            "classic_accuracy": self.classic_accuracy,
            "combatants": {k: asdict(v) for k, v in self.combatants.items()},
            "events": [asdict(e) for e in self.events],
        }


def create_player_combatant(name: str = "Runtime Fighter", level: int = 1, hp: Optional[int] = None,
                            stats: Optional[Dict[str, int]] = None, skills: Optional[Dict[str, int]] = None,
                            equipment_totals: Optional[Dict[str, int]] = None) -> CombatantSnapshot:
    stats = stats or {"str": 60, "agl": 55, "hea": 50}
    max_hp = hp if hp is not None else 20 + (level * 5) + max(0, (stats.get("hea", 50) - 50) // 2)
    return CombatantSnapshot(
        runtime_id="player_" + "".join(ch.lower() if ch.isalnum() else "_" for ch in name).strip("_"),
        name=name,
        kind="player",
        level=level,
        max_hp=max_hp,
        hp=max_hp,
        stats=stats,
        skills=skills or {"weapon": 20},
        equipment_totals=equipment_totals or {"min_hit": 2, "max_hit": 6, "armor_class_bonus": 0, "damage_resistance_bonus": 0},
    )


def create_monster_combatant(monster_record: Optional[Dict[str, Any]] = None, fallback_name: str = "Runtime Goblin") -> CombatantSnapshot:
    m = monster_record or {}
    name = str(m.get("name") or m.get("Name") or fallback_name)
    mid = str(m.get("id") or m.get("monster_id") or name)
    def first_int(*keys: str, default: int) -> int:
        for k in keys:
            try:
                v = m.get(k)
                if v not in (None, ""):
                    return int(v)
            except Exception:
                pass
        return default
    level = first_int("level", "Level", "lvl", default=1)
    hp = first_int("hp", "HP", "hits", "HitPoints", default=8 + level * 4)
    min_hit = first_int("min_hit", "MinHit", "min_damage", "DamageMin", default=1)
    max_hit = first_int("max_hit", "MaxHit", "max_damage", "DamageMax", default=max(2, min_hit + 3))
    ac = first_int("armor_class", "ac", "AC", default=0)
    return CombatantSnapshot(
        runtime_id="monster_" + "".join(ch.lower() if ch.isalnum() else "_" for ch in mid).strip("_"),
        name=name,
        kind="monster",
        level=level,
        max_hp=hp,
        hp=hp,
        stats={"str": first_int("str", "STR", default=50), "agl": first_int("agl", "AGL", default=50), "hea": first_int("hea", "HEA", default=50)},
        skills={"natural": first_int("skill", "attack_skill", default=10 + level)},
        equipment_totals={"min_hit": min_hit, "max_hit": max_hit, "armor_class_bonus": ac, "damage_resistance_bonus": first_int("dr", "DR", default=0)},
        flags={"natural_min_hit": min_hit, "natural_max_hit": max_hit},
    )


def resolve_attack(state: CombatState, attacker_id: str, defender_id: str, rng: Optional[Random] = None, mode: str = "reforged_formula") -> AttackResult:
    rng = rng or Random()
    attacker = state.combatants[attacker_id]
    defender = state.combatants[defender_id]
    engine = default_formula_engine()
    hit_trace = engine.run("reforged_hit_chance_v1", attacker=attacker.to_formula_attacker(), defender=defender.to_formula_defender(), mode=mode)
    hit_chance = float(hit_trace.result)
    attack_roll = rng.randint(1, 100)
    hit = attack_roll <= hit_chance
    traces = [hit_trace]
    weapon_roll = 0
    damage = 0
    if hit:
        low, high = attacker.weapon_damage_range()
        weapon_roll = rng.randint(low, high)
        dmg_trace = engine.run("reforged_damage_v1", attacker=attacker.to_formula_attacker(), defender=defender.to_formula_defender(), weapon_roll=weapon_roll, mode=mode)
        damage = int(dmg_trace.result)
        defender.hp = max(0, defender.hp - damage)
        traces.append(dmg_trace)
        state.add_event("attack_hit", attacker_id, defender_id, f"{attacker.name} hits {defender.name} for {damage} damage.", {
            "attack_roll": attack_roll, "hit_chance": hit_chance, "weapon_roll": weapon_roll, "damage": damage, "defender_hp_after": defender.hp,
            "classic_accuracy": "not_classic_claimed",
        })
        if defender.hp <= 0:
            state.add_event("death", defender_id, None, f"{defender.name} is defeated.", {"killer_id": attacker_id})
    else:
        state.add_event("attack_miss", attacker_id, defender_id, f"{attacker.name} misses {defender.name}.", {
            "attack_roll": attack_roll, "hit_chance": hit_chance, "classic_accuracy": "not_classic_claimed",
        })
    return AttackResult(attacker_id, defender_id, hit, attack_roll, hit_chance, weapon_roll, damage, defender.hp, traces)


def _initiative_key(combatant: CombatantSnapshot, rng: Random) -> Tuple[int, int]:
    speed = int(combatant.equipment_totals.get("speed", 2000))
    agility = int(combatant.stats.get("agl", 50))
    # Lower weapon speed is better; higher agility and random tie roll are better.
    return (-(2000 - speed + agility), -rng.randint(1, 1000))


def run_combat_round(state: CombatState, rng: Optional[Random] = None, mode: str = "reforged_formula") -> List[AttackResult]:
    rng = rng or Random()
    state.round_number += 1
    actors = [c for c in state.combatants.values() if c.alive]
    actors.sort(key=lambda c: _initiative_key(c, rng))
    results: List[AttackResult] = []
    for actor in actors:
        if not actor.alive:
            continue
        targets = state.living_enemies_of(actor)
        if not targets:
            continue
        target = sorted(targets, key=lambda c: (c.hp, c.runtime_id))[0]
        results.append(resolve_attack(state, actor.runtime_id, target.runtime_id, rng=rng, mode=mode))
    return results


def run_duel(player: CombatantSnapshot, monster: CombatantSnapshot, seed: int = 1, max_rounds: int = 50, mode: str = "reforged_formula") -> CombatState:
    state = CombatState(combatants={player.runtime_id: player, monster.runtime_id: monster}, mode=mode)
    rng = Random(seed)
    state.add_event("combat_start", player.runtime_id, monster.runtime_id, f"Combat starts: {player.name} vs {monster.name}.", {"seed": seed, "mode": mode})
    while player.alive and monster.alive and state.round_number < max_rounds:
        run_combat_round(state, rng=rng, mode=mode)
    if player.alive and not monster.alive:
        state.add_event("combat_end", player.runtime_id, monster.runtime_id, f"{player.name} wins.", {"winner_id": player.runtime_id})
    elif monster.alive and not player.alive:
        state.add_event("combat_end", monster.runtime_id, player.runtime_id, f"{monster.name} wins.", {"winner_id": monster.runtime_id})
    else:
        state.add_event("combat_end", "system", None, "Combat ended by round limit.", {"winner_id": None})
    return state
