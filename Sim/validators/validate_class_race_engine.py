from __future__ import annotations

import json
from pathlib import Path

from Runtime.class_race_engine import ClassRaceEngine, CANONICAL_STATS


def validate_class_race_engine(repo_root: Path) -> dict:
    passed = 0
    failed = 0
    unknown = 0
    notes = []
    data_dir = repo_root / "Data"
    try:
        engine = ClassRaceEngine(data_dir)
        summary = engine.summarize()
        if summary["class_count"] == 15:
            passed += 1
        else:
            failed += 1
            notes.append(f"Expected 15 classes; found {summary['class_count']}")
        if summary["race_count"] == 13:
            passed += 1
        else:
            failed += 1
            notes.append(f"Expected 13 races; found {summary['race_count']}")

        # Validate all race stat bounds have canonical stat keys.
        missing_bounds = []
        for race in engine.list_races():
            for stat in CANONICAL_STATS:
                if stat not in race.min_stats or stat not in race.max_stats:
                    missing_bounds.append(f"{race.name}:{stat}")
        if not missing_bounds:
            passed += 1
        else:
            failed += 1
            notes.append("Missing race stat bounds: " + ", ".join(missing_bounds[:20]))

        # Build a representative profile from actual data.
        first_class = engine.list_classes()[0]
        first_race = engine.list_races()[0]
        stats = {stat: int(first_race.min_stats.get(stat, 0)) for stat in CANONICAL_STATS}
        profile = engine.build_profile("RuntimeTester", first_class.id, first_race.id, 1, stats)
        if profile.class_id == first_class.id and profile.race_id == first_race.id and not profile.validation_warnings:
            passed += 1
        else:
            failed += 1
            notes.append("Representative class/race profile did not validate cleanly")

        # Verify invalid stats are not silently accepted as clean.
        bad_stats = {stat: -999 for stat in CANONICAL_STATS}
        bad_profile = engine.build_profile("InvalidTester", first_class.id, first_race.id, 1, bad_stats)
        if bad_profile.validation_warnings:
            passed += 1
        else:
            failed += 1
            notes.append("Invalid race stats produced no validation warnings")

        # Write report artifact.
        report_path = repo_root / "Reports" / "class_race_engine_report_0013.json"
        report_path.write_text(json.dumps({
            "milestone": "Runtime-0013A-Class-Race-Engine",
            "status": "PASS" if failed == 0 else "FAIL",
            "summary": summary,
            "representative_profile": profile.to_dict(),
            "formula_policy": "No unverified classic formulas promoted.",
        }, indent=2), encoding="utf-8")

        unknown += 1
        notes.append("Class/race hidden passives and exact classic progression formulas remain UNKNOWN until evidence-backed.")
    except Exception as exc:
        failed += 1
        notes.append(str(exc))

    return {
        "status": "PASS" if failed == 0 else "FAIL",
        "passed": passed,
        "failed": failed,
        "unknown": unknown,
        "notes": notes,
    }
