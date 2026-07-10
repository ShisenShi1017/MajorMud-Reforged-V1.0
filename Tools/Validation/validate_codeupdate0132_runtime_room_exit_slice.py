#!/usr/bin/env python3
"""Validate CodeUpdate0132 runtime room/exit load slice.

No runtime execution is required. This verifies that generated Classic room data now
contains a named start room, embedded movement exits, and resolvable target rooms.
"""
from __future__ import annotations
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ROOMS = ROOT / "Data" / "Classic" / "rooms.json"
EXITS = ROOT / "Data" / "Classic" / "exits.json"
REPORT = ROOT / "Reports" / "CodeUpdate0132" / "runtime_room_exit_slice_0132.json"


def load_records(path: Path):
    obj = json.loads(path.read_text(encoding="utf-8"))
    return obj.get("records", obj if isinstance(obj, list) else [])


def main() -> int:
    issues: list[str] = []
    if not ROOMS.exists():
        issues.append(f"missing {ROOMS}")
        print(json.dumps({"status": "FAIL", "issues": issues}, indent=2))
        return 1
    rooms = load_records(ROOMS)
    by_id = {room.get("id"): room for room in rooms if room.get("id")}
    by_number = {}
    for room in rooms:
        number = room.get("room_number") or room.get("roomNumber") or room.get("legacy_id")
        try:
            by_number[int(number)] = room
        except Exception:
            pass

    start = by_id.get("room.1")
    if start is None:
        issues.append("start room room.1 is missing")
    else:
        if not start.get("name") or start.get("name") == "Unknown Room":
            issues.append(f"start room has invalid name: {start.get('name')!r}")
        exits = start.get("exits") or []
        if not exits:
            issues.append("start room has no embedded exits")
        for exit_record in exits:
            target_number = exit_record.get("to_room") or exit_record.get("targetRoomNumber")
            if target_number not in by_number:
                issues.append(f"start exit {exit_record.get('dir')} points to unresolved room {target_number}")
                continue
            target = by_number[target_number]
            if not target.get("name") or target.get("name") == "Unknown Room":
                issues.append(f"start exit {exit_record.get('dir')} points to unnamed room {target.get('id')}")

    exit_records = load_records(EXITS) if EXITS.exists() else []
    rooms_with_exits = sum(1 for room in rooms if room.get("exits"))
    result = {
        "code_update": "0132",
        "status": "PASS" if not issues else "FAIL",
        "rooms": len(rooms),
        "rooms_with_names": sum(1 for room in rooms if room.get("name") and room.get("name") != "Unknown Room"),
        "rooms_with_embedded_exits": rooms_with_exits,
        "exit_records": len(exit_records),
        "start_room": {
            "id": start.get("id") if start else None,
            "name": start.get("name") if start else None,
            "exit_count": len(start.get("exits") or []) if start else 0,
            "exits": [f"{exit_record.get('dir')}->{exit_record.get('to_room') or exit_record.get('targetRoomNumber')}" for exit_record in (start.get("exits") or [])] if start else [],
        },
        "issues": issues,
    }
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(result, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0 if not issues else 1


if __name__ == "__main__":
    sys.exit(main())
