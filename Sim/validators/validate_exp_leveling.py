from __future__ import annotations

import json
from pathlib import Path

from Runtime.character_snapshot import create_character_snapshot
from Runtime.leveling import (
    apply_leveling_to_snapshot,
    calculate_level_from_exp,
    class_exp_modifier,
    cp_per_level,
    race_exp_modifier,
    reforged_exp_required_for_level,
)


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def validate_exp_leveling(repo_root: Path):
    passed = 0
    failed = 0
    unknown = 0
    notes = []
    data_dir = repo_root / "Data"

    try:
        leveling = _load_json(data_dir / "leveling.json")
        classes = _load_json(data_dir / "classs.json")
        races = _load_json(data_dir / "races.json")
        passed += 3
    except Exception as exc:
        return {"status": "FAIL", "passed": passed, "failed": 1, "unknown": 0, "notes": [f"Failed to load progression data: {exc}"]}

    t1 = reforged_exp_required_for_level(1)
    t2 = reforged_exp_required_for_level(2)
    t5 = reforged_exp_required_for_level(5)
    if t1.result == 0 and t2.result > t1.result and t5.result > t2.result:
        passed += 1
    else:
        failed += 1
        notes.append("EXP curve is not monotonic for test levels 1, 2, and 5.")

    if cp_per_level(leveling, 2) >= 0:
        passed += 1
    else:
        failed += 1
        notes.append("CP per level returned a negative value.")

    warrior_mod = class_exp_modifier(classes, "Warrior")
    dwarf_mod = race_exp_modifier(races, "Dwarf")
    if isinstance(warrior_mod, int) and isinstance(dwarf_mod, int):
        passed += 1
    else:
        failed += 1
        notes.append("Class/race EXP modifiers did not resolve as integers.")

    result = calculate_level_from_exp(
        current_exp=10000,
        current_level=1,
        leveling_config=leveling,
        classes=classes,
        races=races,
        class_id="Warrior",
        race_id="Dwarf",
    )
    if result.target_level >= 1 and result.exp_for_current_level <= result.current_exp:
        passed += 1
    else:
        failed += 1
        notes.append("Level calculation returned an invalid target level for sample snapshot.")

    snapshot = create_character_snapshot(name="Runtime Level Test", race_id="Dwarf", class_id="Warrior", level=1)
    apply_leveling_to_snapshot(snapshot, result)
    if snapshot.level == result.target_level and snapshot.current.get("exp") == 10000:
        passed += 1
    else:
        failed += 1
        notes.append("Leveling result failed to apply to character snapshot.")

    unknown += 1
    notes.append("Exact classic MajorMUD EXP/leveling parity remains UNKNOWN until formula evidence is proven.")

    return {
        "status": "PASS" if failed == 0 else "FAIL",
        "passed": passed,
        "failed": failed,
        "unknown": unknown,
        "notes": notes,
    }
