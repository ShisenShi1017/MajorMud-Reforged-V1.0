"""Validate Runtime-0009 executable combat runtime."""
from __future__ import annotations

import json
from pathlib import Path

from Runtime.combat import create_player_combatant, create_monster_combatant, resolve_attack, run_duel


def _load_first_monster(repo_root: Path):
    path = repo_root / "Data" / "monsters.json"
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None
    if isinstance(data, list) and data:
        return data[0]
    if isinstance(data, dict):
        for key in ("records", "monsters", "data", "items"):
            value = data.get(key)
            if isinstance(value, list) and value:
                return value[0]
    return None


def validate_combat(repo_root):
    repo_root = Path(repo_root)
    passed = failed = unknown = 0
    notes = []

    player = create_player_combatant(
        name="Runtime 0009 Fighter",
        level=4,
        hp=45,
        stats={"str": 65, "agl": 60, "hea": 55},
        skills={"sword": 35},
        equipment_totals={"min_hit": 4, "max_hit": 9, "armor_class_bonus": 8, "damage_resistance_bonus": 1, "speed": 1800},
    )
    monster = create_monster_combatant(_load_first_monster(repo_root), fallback_name="Runtime 0009 Monster")
    monster.hp = min(monster.hp, 18)
    monster.max_hp = monster.hp

    try:
        state = run_duel(player, monster, seed=9, max_rounds=20)
        if state.round_number > 0 and state.events:
            passed += 1
            notes.append(f"Executable duel PASS; rounds={state.round_number}; events={len(state.events)}")
        else:
            failed += 1
            notes.append("Executable duel FAIL; no rounds/events produced.")

        attack_events = [e for e in state.events if e.event_type in ("attack_hit", "attack_miss")]
        if attack_events:
            passed += 1
            notes.append(f"Attack event pipeline PASS; attack events={len(attack_events)}")
        else:
            failed += 1
            notes.append("Attack event pipeline FAIL; no attack hit/miss events.")

        if state.classic_accuracy == "not_classic_claimed" and all(e.data.get("classic_accuracy", "not_classic_claimed") == "not_classic_claimed" for e in attack_events):
            passed += 1
            notes.append("Combat provenance PASS; no classic accuracy claimed for Reforged formulas.")
        else:
            failed += 1
            notes.append("Combat provenance FAIL; classic accuracy was claimed unexpectedly.")

        # Direct attack test verifies trace capture and damage clamp.
        p2 = create_player_combatant(name="Direct Attacker", hp=30, equipment_totals={"min_hit": 1, "max_hit": 1, "damage_resistance_bonus": 0})
        m2 = create_monster_combatant(None, fallback_name="Direct Defender")
        m2.hp = 10
        m2.max_hp = 10
        from Runtime.combat.combat_runtime import CombatState
        cstate = CombatState({p2.runtime_id: p2, m2.runtime_id: m2})
        result = resolve_attack(cstate, p2.runtime_id, m2.runtime_id, __import__('random').Random(1))
        if result.traces and 5 <= result.hit_chance <= 95 and result.defender_hp_after <= 10:
            passed += 1
            notes.append(f"Attack resolution PASS; hit={result.hit}; roll={result.attack_roll}; chance={result.hit_chance}; damage={result.damage}")
        else:
            failed += 1
            notes.append("Attack resolution FAIL; trace/chance/hp invariant failed.")

    except Exception as exc:
        failed += 1
        notes.append(f"Combat execution ERROR: {exc}")

    unknown += 1
    notes.append("Classic hit, damage, crit, dual-wield, backstab, status, and spell-combat formulas remain UNKNOWN until evidence-backed.")

    return {"status": "PASS" if failed == 0 else "FAIL", "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
