from Runtime.formula_engine import default_formula_engine


def validate_formula_engine(repo_root):
    engine = default_formula_engine(seed=1)
    passed = failed = unknown = 0
    notes = []
    attacker = {"weapon_skill": 20, "accuracy_stat": 15, "level": 5, "item_accuracy_bonus": 2}
    defender = {"defense": 18, "agility": 10, "level": 4, "armor_defense_bonus": 3}
    hit = engine.run("reforged_hit_chance_v1", attacker=attacker, defender=defender)
    if 5 <= hit.result <= 95 and hit.classic_accuracy == "not_classic_claimed":
        passed += 1
        notes.append(f"Hit formula trace OK; result={hit.result}; classic accuracy not claimed.")
    else:
        failed += 1
        notes.append("Hit formula failed clamp or provenance check.")
    dmg = engine.run("reforged_damage_v1", attacker={"strength_bonus": 2}, defender={"damage_reduction": 99}, weapon_roll=4)
    if dmg.result == 1 and dmg.classic_accuracy == "not_classic_claimed":
        passed += 1
        notes.append("Damage formula trace OK; minimum damage enforced; classic accuracy not claimed.")
    else:
        failed += 1
        notes.append("Damage formula failed minimum/provenance check.")
    unknown += 1
    notes.append("Classic formula slots remain UNKNOWN until evidence-backed derivation is added.")
    return {"status": "PASS" if failed == 0 else "FAIL", "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
