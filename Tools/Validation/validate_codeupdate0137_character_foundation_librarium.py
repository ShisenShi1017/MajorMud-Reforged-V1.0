#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def main() -> int:
    subprocess.run(
        [sys.executable, str(ROOT / "Tools" / "Librarium" / "compile_character_foundation_0137.py"), "--root", str(ROOT)],
        check=True,
    )
    foundation = json.loads((ROOT / "Data" / "Classic" / "character_foundation.json").read_text(encoding="utf-8"))
    errors: list[str] = []
    if foundation.get("authority") != "Librarium":
        errors.append("character foundation authority is not Librarium")
    if foundation.get("race_count") != 13 or len(foundation.get("races", [])) != 13:
        errors.append("expected 13 SQL/MDB-backed playable races")
    if foundation.get("class_count") != 15 or len(foundation.get("classes", [])) != 15:
        errors.append("expected 15 SQL/MDB-backed playable classes")
    if foundation.get("compatibility_count") != 195 or len(foundation.get("compatibility", [])) != 195:
        errors.append("expected all 13x15 race/class compatibility pairs to be classified")
    for record in foundation.get("races", []) + foundation.get("classes", []):
        if not str(record.get("source_file", "")).startswith("Librarium/Objects/"):
            errors.append(f"non-Librarium source file recorded for {record.get('id')}")
        if record.get("classification") != "playable_classic_catalog_record":
            errors.append(f"unexpected catalog classification for {record.get('id')}")
    class_names = {record.get("name", "").lower() for record in foundation.get("classes", [])}
    for false_candidate in ("training room", "guild", "trainer"):
        if any(false_candidate in name for name in class_names):
            errors.append(f"false class candidate promoted from {false_candidate} evidence")

    if errors:
        print("FAIL CodeUpdate0137 Librarium character foundation")
        for error in errors:
            print(f"- {error}")
        return 1
    print("PASS CodeUpdate0137 Librarium character foundation")
    print("13 races; 15 classes; 195 compatibility pairs classified without guessed runtime allowance")
    print("BLOCKED: exact compatibility, starting stats, skills, spells, equipment, HP, mana, and alignment remain evidence work")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
