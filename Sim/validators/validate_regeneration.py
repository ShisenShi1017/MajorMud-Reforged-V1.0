from __future__ import annotations

import json
from pathlib import Path

from Runtime.character_snapshot import create_character_snapshot
from Runtime.leveling import calculate_level_from_exp
from Runtime.regeneration import (
    apply_regeneration_tick_to_snapshot,
    apply_vital_pools_to_snapshot,
    calculate_regeneration_tick,
    calculate_vital_pools,
)


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def validate_regeneration(repo_root: Path):
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
        return {"status": "FAIL", "passed": passed, "failed": 1, "unknown": 0, "notes": [f"Failed to load vital progression data: {exc}"]}

    snapshot = create_character_snapshot(
        name="Runtime Regen Test",
        race_id="Dwarf",
        class_id="Warrior",
        level=1,
        base_stats={"str": 55, "int": 45, "wil": 50, "agl": 50, "hea": 65, "cha": 40},
    )
    level_result = calculate_level_from_exp(10000, 1, leveling, classes, races, "Warrior", "Dwarf")
    snapshot.level = level_result.target_level
    snapshot.current["cp_awarded_from_levels"] = level_result.total_cp_awarded

    pools = calculate_vital_pools(snapshot, classes=classes, races=races)
    if pools.max_hp >= 1 and pools.current_hp <= pools.max_hp and pools.current_mana <= pools.max_mana:
        passed += 1
    else:
        failed += 1
        notes.append("Vital pool calculation returned invalid max/current values.")

    snapshot.current["hp"] = max(0, pools.max_hp - 10)
    snapshot.current["mana"] = max(0, pools.max_mana - 8)
    snapshot.current["cp"] = max(0, pools.max_cp - 1)
    injured_pools = calculate_vital_pools(snapshot, classes=classes, races=races)
    tick = calculate_regeneration_tick(snapshot, injured_pools, resting=False, in_combat=False)
    if tick.hp_after >= tick.hp_before and tick.mana_after >= tick.mana_before:
        passed += 1
    else:
        failed += 1
        notes.append("Normal regeneration tick reduced HP or mana unexpectedly.")

    resting_tick = calculate_regeneration_tick(snapshot, injured_pools, resting=True, in_combat=False)
    if resting_tick.hp_gained >= tick.hp_gained and resting_tick.mana_gained >= tick.mana_gained:
        passed += 1
    else:
        failed += 1
        notes.append("Resting regeneration did not meet or exceed normal regeneration.")

    capped_snapshot = create_character_snapshot(name="Runtime Regen Cap Test", level=5)
    capped_pools = calculate_vital_pools(capped_snapshot, classes=classes, races=races)
    cap_tick = calculate_regeneration_tick(capped_snapshot, capped_pools, resting=True)
    if cap_tick.hp_after <= capped_pools.max_hp and cap_tick.mana_after <= capped_pools.max_mana:
        passed += 1
    else:
        failed += 1
        notes.append("Regeneration exceeded calculated pool caps.")

    apply_vital_pools_to_snapshot(snapshot, injured_pools)
    apply_regeneration_tick_to_snapshot(snapshot, tick)
    if snapshot.current.get("hp") == tick.hp_after and snapshot.current.get("mana") == tick.mana_after:
        passed += 1
    else:
        failed += 1
        notes.append("Regeneration tick failed to apply to character snapshot.")

    unknown += 1
    notes.append("Exact classic MajorMUD HP/mana/CP and regeneration formulas remain UNKNOWN until formula evidence is proven.")

    return {
        "status": "PASS" if failed == 0 else "FAIL",
        "passed": passed,
        "failed": failed,
        "unknown": unknown,
        "notes": notes,
    }
