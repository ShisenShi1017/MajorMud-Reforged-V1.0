"""Validate Runtime-0003 character snapshot and equipment stat calculator."""
from __future__ import annotations

from pathlib import Path

from Runtime.character_snapshot import create_character_snapshot
from Runtime.equipment_stats import calculate_equipment_stats

TEST_WEAPON = "item_1622_abyssal_scimitar"
TEST_ARMOR = "item_423_adamantite_bracers"


def validate_snapshot_equipment(repo_root=None):
    repo_root = Path(repo_root or ".")
    data_dir = repo_root / "Data"
    passed = failed = unknown = 0
    notes = []

    snap = create_character_snapshot(
        name="Runtime 0003 Test",
        race_id="test_race_unverified",
        class_id="test_class_unverified",
        level=5,
        base_stats={"str": 60, "agl": 55, "hea": 50, "int": 40, "wil": 45, "cha": 35},
        equipment={"weapon": TEST_WEAPON, "wrists": TEST_ARMOR},
        skills={"sword": 25},
    )

    if snap.level == 5 and snap.base_stats["str"] == 60 and snap.provenance.get("classic_accuracy") == "not_classic_claimed":
        passed += 1
        notes.append("Character snapshot creation PASS; classic derived values not claimed.")
    else:
        failed += 1
        notes.append("Character snapshot creation FAIL.")

    trace = calculate_equipment_stats(snap, str(data_dir))
    totals = trace.totals
    expected = {
        "min_hit": 14,
        "max_hit": 33,
        "speed": 1800,
        "armor_class_bonus": 20,
        "damage_resistance_bonus": 5,
        "equipped_count": 2,
    }
    mismatches = []
    for key, value in expected.items():
        if totals.get(key) != value:
            mismatches.append(f"{key}: got {totals.get(key)} expected {value}")
    if not mismatches and trace.classic_accuracy == "not_classic_claimed":
        passed += 1
        notes.append(f"Equipment explicit-field aggregation PASS; totals={totals}")
    else:
        failed += 1
        notes.append("Equipment explicit-field aggregation FAIL: " + "; ".join(mismatches))

    if trace.issues:
        unknown += len([i for i in trace.issues if i.severity in ("info", "warning")])
        notes.append(f"Equipment unresolved/provenance issues retained: {len(trace.issues)}")

    unknown += 1
    notes.append("Slot legality, ability effects, class/race restrictions, and classic derived stat formulas remain UNKNOWN until evidence-backed.")

    return {"status": "PASS" if failed == 0 else "FAIL", "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
