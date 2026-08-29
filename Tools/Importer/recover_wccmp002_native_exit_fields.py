#!/usr/bin/env python3
"""Recover exact native room-exit fields from stock WCCMP002.DAT.

The MMExplorer Rooms projection retains semantic exit text but loses some raw
RoomRec fields, most importantly trap Para2.  Canonical room records already
retain a verified WCCMP002 native_record_offset, so no physical Btrieve page
parser is required: each room payload can be read directly at that offset.

This pass validates every retained room offset, decodes the mixed-width native
exit arrays, and projects exact Para2 values into canonical trap effects as
``native_trap_state``.  It also emits a compact evidence ledger for the 295
canonical traps.  Other native exit fields are validated and summarized here;
they are intentionally not bulk-copied into every generated exit record.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from collections import Counter
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
ROOMS_MANIFEST = Path("Librarium/Objects/rooms/mmexplorer_room_records_0140.json")
EXITS_MANIFEST = Path("Librarium/Objects/exits/mmexplorer_exit_records_0140.json")
DEFAULT_SOURCE = Path("ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat")
EVIDENCE_OUTPUT = Path("Librarium/Parity/majormud_wccmp002_native_exit_recovery_0333.json")
REPORT_OUTPUT = Path("Reports/RuntimeVerification/wccmp002_native_exit_recovery.md")

DIRECTION_INDEX = {"N": 0, "S": 1, "E": 2, "W": 3, "NE": 4, "NW": 5, "SE": 6, "SW": 7, "U": 8, "D": 9}
TRAP_STATE_NAMES = {0: "Active", 1: "Inactive", 2: "Hidden", 3: "Active movement", 4: "Inactive movement"}
ROOM_EXIT_OFFSET = 824
ROOM_TYPE_OFFSET = 864
PARA1_OFFSET = 884
PARA2_OFFSET = 924
PARA3_OFFSET = 944
PARA4_OFFSET = 984
ROOM_RECORD_SIZE = 1544


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, indent=2)
    # Preserve the HTML-safe escaping used by the canonical .NET-generated Librarium chunks
    # so this recovery pass changes only the records that actually gain native evidence.
    text = text.replace("<", "\\u003C").replace(">", "\\u003E").replace("&", "\\u0026")
    path.write_text(text + "\n", encoding="utf-8")


def iter_chunked_records(manifest_path: Path) -> list[dict[str, Any]]:
    root = load_json(manifest_path)
    if isinstance(root, list):
        return [row for row in root if isinstance(row, dict)]
    if isinstance(root.get("records"), list):
        return [row for row in root["records"] if isinstance(row, dict)]
    rows: list[dict[str, Any]] = []
    for relative in root.get("record_parts", []):
        part = load_json(manifest_path.parent / relative)
        values = part.get("records", []) if isinstance(part, dict) else part
        rows.extend(row for row in values if isinstance(row, dict))
    return rows


def load_exit_parts(manifest_path: Path) -> list[tuple[Path, dict[str, Any]]]:
    root = load_json(manifest_path)
    if isinstance(root.get("records"), list):
        return [(manifest_path, root)]
    result: list[tuple[Path, dict[str, Any]]] = []
    for relative in root.get("record_parts", []):
        path = manifest_path.parent / relative
        part = load_json(path)
        if not isinstance(part, dict) or not isinstance(part.get("records"), list):
            raise RuntimeError(f"Invalid exit record part: {path}")
        result.append((path, part))
    return result


def s16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<h", data, offset)[0]


def s32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<i", data, offset)[0]


def native_fields(data: bytes, record_offset: int, index: int) -> dict[str, int]:
    return {
        "room_exit": s32(data, record_offset + ROOM_EXIT_OFFSET + 4 * index),
        "room_type": s16(data, record_offset + ROOM_TYPE_OFFSET + 2 * index),
        "para1": s32(data, record_offset + PARA1_OFFSET + 4 * index),
        "para2": s16(data, record_offset + PARA2_OFFSET + 2 * index),
        "para3": s32(data, record_offset + PARA3_OFFSET + 4 * index),
        "para4": s32(data, record_offset + PARA4_OFFSET + 4 * index),
    }


def is_trap(exit_record: dict[str, Any]) -> tuple[bool, int, int]:
    effects = exit_record.get("effects") if isinstance(exit_record.get("effects"), dict) else {}
    damage = int(effects.get("damage") or 0)
    spell_id = int(effects.get("spell_id") or 0)
    return damage > 0 or spell_id > 0, damage, spell_id


def recover(repo: Path, source_path: Path, apply: bool) -> dict[str, Any]:
    data = source_path.read_bytes()
    rooms = iter_chunked_records(repo / ROOMS_MANIFEST)
    room_by_id = {(int(row["map_number"]), int(row["room_number"])): row for row in rooms}

    valid_room_offsets = 0
    room_offset_errors: list[dict[str, Any]] = []
    for key, room in room_by_id.items():
        placed = room.get("placed_definition") if isinstance(room.get("placed_definition"), dict) else {}
        offset = placed.get("native_record_offset")
        if not isinstance(offset, int) or offset < 0 or offset + ROOM_RECORD_SIZE > len(data):
            room_offset_errors.append({"room": f"{key[0]}/{key[1]}", "error": "missing-or-invalid-native-record-offset"})
            continue
        native_key = (s32(data, offset), s32(data, offset + 4))
        if native_key != key:
            room_offset_errors.append({"room": f"{key[0]}/{key[1]}", "native_room": f"{native_key[0]}/{native_key[1]}", "offset": offset})
            continue
        valid_room_offsets += 1
    if room_offset_errors:
        raise RuntimeError(f"WCCMP002 room offset validation failed for {len(room_offset_errors)} room(s): {room_offset_errors[:5]}")

    exit_parts = load_exit_parts(repo / EXITS_MANIFEST)
    trap_rows: list[dict[str, Any]] = []
    trap_errors: list[dict[str, Any]] = []
    destination_mismatches = 0
    destination_mismatch_rows: list[dict[str, Any]] = []
    native_type_counts: Counter[int] = Counter()
    matching_native_exits = 0
    total_exits = 0

    for part_path, part in exit_parts:
        changed = False
        for exit_record in part["records"]:
            total_exits += 1
            key = (int(exit_record["from_map_number"]), int(exit_record["from_room_number"]))
            room = room_by_id.get(key)
            if room is None:
                continue
            direction = str(exit_record.get("direction") or "").upper()
            if direction not in DIRECTION_INDEX:
                continue
            index = DIRECTION_INDEX[direction]
            record_offset = room["placed_definition"]["native_record_offset"]
            fields = native_fields(data, record_offset, index)
            native_type_counts[fields["room_type"]] += 1
            if fields["room_exit"] == int(exit_record.get("to_room_number") or 0):
                matching_native_exits += 1
            else:
                destination_mismatches += 1
                destination_mismatch_rows.append({
                    "exit_id": exit_record.get("id"),
                    "from_map_number": key[0],
                    "from_room_number": key[1],
                    "direction": direction,
                    "native_exit_index": index,
                    "canonical_to_map_number": int(exit_record.get("to_map_number") or 0),
                    "canonical_to_room_number": int(exit_record.get("to_room_number") or 0),
                    "native_to_room_number": fields["room_exit"],
                    "native_room_type": fields["room_type"],
                    "native_para1": fields["para1"],
                    "native_para2": fields["para2"],
                    "native_para3": fields["para3"],
                    "native_para4": fields["para4"],
                })

            trap, damage, spell_id = is_trap(exit_record)
            if not trap:
                continue
            expected_type = 9 if damage > 0 else 24
            expected_operand = damage if damage > 0 else spell_id
            errors: list[str] = []
            if fields["room_exit"] != int(exit_record["to_room_number"]):
                errors.append(f"destination {fields['room_exit']} != {exit_record['to_room_number']}")
            if fields["room_type"] != expected_type:
                errors.append(f"room_type {fields['room_type']} != {expected_type}")
            if fields["para1"] != expected_operand:
                errors.append(f"para1 {fields['para1']} != {expected_operand}")
            if fields["para2"] not in TRAP_STATE_NAMES:
                errors.append(f"unsupported trap Para2 {fields['para2']}")
            if errors:
                trap_errors.append({"exit_id": exit_record.get("id"), "errors": errors, "native": fields})
                continue

            effects = exit_record.setdefault("effects", {})
            effects["native_trap_state"] = fields["para2"]
            exit_record["native_exit_record"] = {
                "source": "WCCMP002",
                "native_record_offset": record_offset,
                "native_exit_index": index,
                "room_type": fields["room_type"],
                "para1": fields["para1"],
                "para2": fields["para2"],
                "para3": fields["para3"],
                "para4": fields["para4"],
            }
            evidence_refs = exit_record.setdefault("evidence_refs", [])
            marker = "direct_wccmp002_native_room_record_decode"
            if not any(isinstance(row, dict) and row.get("classification") == marker for row in evidence_refs):
                evidence_refs.append({
                    "source": "WCCMP002",
                    "path": str(DEFAULT_SOURCE).replace("\\", "/"),
                    "native_record_offset": record_offset,
                    "native_exit_index": index,
                    "classification": marker,
                })
            changed = True
            trap_rows.append({
                "exit_id": exit_record["id"],
                "from_map_number": key[0],
                "from_room_number": key[1],
                "direction": direction,
                "native_exit_index": index,
                "to_room_number": fields["room_exit"],
                "room_type": fields["room_type"],
                "trap_kind": "damage_trap" if fields["room_type"] == 9 else "spell_trap",
                "para1": fields["para1"],
                "para2": fields["para2"],
                "para2_name": TRAP_STATE_NAMES[fields["para2"]],
                "para3": fields["para3"],
                "para4": fields["para4"],
                "native_record_offset": record_offset,
                "native_para2_file_offset": record_offset + PARA2_OFFSET + 2 * index,
            })
        if apply and changed:
            write_json(part_path, part)

    if trap_errors:
        raise RuntimeError(f"WCCMP002 trap validation failed for {len(trap_errors)} exit(s): {trap_errors[:5]}")
    if len(trap_rows) != 295:
        raise RuntimeError(f"Expected 295 canonical traps but recovered {len(trap_rows)}")
    damage_count = sum(row["trap_kind"] == "damage_trap" for row in trap_rows)
    spell_count = sum(row["trap_kind"] == "spell_trap" for row in trap_rows)
    if (damage_count, spell_count) != (273, 22):
        raise RuntimeError(f"Expected 273 damage + 22 spell traps but recovered {damage_count} + {spell_count}")

    state_counts = Counter(row["para2"] for row in trap_rows)
    evidence = {
        "schema": "majormud.wccmp002-native-exit-recovery.v1",
        "update": "0333",
        "source_file": str(DEFAULT_SOURCE).replace("\\", "/"),
        "source_sha256": hashlib.sha256(data).hexdigest(),
        "source_size": len(data),
        "room_record_layout": {
            "record_size": ROOM_RECORD_SIZE,
            "RoomExit": {"offset": ROOM_EXIT_OFFSET, "width": 4, "count": 10, "signed": True},
            "RoomType": {"offset": ROOM_TYPE_OFFSET, "width": 2, "count": 10, "signed": True},
            "Para1": {"offset": PARA1_OFFSET, "width": 4, "count": 10, "signed": True},
            "Para2": {"offset": PARA2_OFFSET, "width": 2, "count": 10, "signed": True},
            "Para3": {"offset": PARA3_OFFSET, "width": 4, "count": 10, "signed": True},
            "Para4": {"offset": PARA4_OFFSET, "width": 4, "count": 10, "signed": True},
        },
        "direction_slot_mapping": DIRECTION_INDEX,
        "validated_room_record_count": valid_room_offsets,
        "canonical_exit_count": total_exits,
        "canonical_exit_destination_matches": matching_native_exits,
        "canonical_exit_destination_mismatches": destination_mismatches,
        "canonical_exit_destination_mismatch_records": destination_mismatch_rows,
        "native_room_type_distribution": {str(k): v for k, v in sorted(native_type_counts.items())},
        "trap_count": len(trap_rows),
        "damage_trap_count": damage_count,
        "spell_trap_count": spell_count,
        "trap_para2_distribution": {str(k): v for k, v in sorted(state_counts.items())},
        "trap_validation_errors": trap_errors,
        "runtime_policy": "Project exact native trap Para2 values, but keep Classic trap execution fail-closed until trigger/transition/reset ordering is proven.",
        "records": trap_rows,
    }
    if apply:
        write_json(repo / EVIDENCE_OUTPUT, evidence)
        report = [
            "# WCCMP002 Native Exit Recovery — Code Update 0333",
            "",
            f"- Source SHA-256: `{evidence['source_sha256']}`",
            f"- Validated room record offsets: **{valid_room_offsets:,}**",
            f"- Canonical exits checked: **{total_exits:,}**",
            f"- Native destination matches: **{matching_native_exits:,}**",
            f"- Known cross-version/legacy destination mismatches retained without overwrite: **{destination_mismatches}**",
            f"- Canonical traps recovered: **{len(trap_rows)}** (**{damage_count} damage + {spell_count} spell**)",
            f"- Trap Para2 states: **{state_counts.get(0, 0)} Active (0)**, **{state_counts.get(1, 0)} Inactive (1)**, **{state_counts.get(2, 0)} Hidden (2)**, **{state_counts.get(3, 0)} Active movement (3)**, **{state_counts.get(4, 0)} Inactive movement (4)**",
            "",
            "The exact native Para2 value is now attached to every canonical Classic trap as `effects.native_trap_state`. Runtime effect execution remains fail-closed because exact SEARCH/DISARM rolls, failed-disarm triggering, state transitions, reset/rearm behavior, and simultaneous movement/death ordering are separate unresolved behaviors.",
            "",
            "## Native/canonical destination review queue",
            "",
            "The 20 destination mismatches are retained as evidence and are **not** auto-overwritten. They include native zero/unlinked slots and cross-version/legacy destination differences that require owner-specific review.",
            "",
        ]
        for row in destination_mismatch_rows:
            report.append(
                f"- `{row['exit_id']}`: canonical room {row['canonical_to_room_number']} vs native RoomExit {row['native_to_room_number']} "
                f"(RoomType {row['native_room_type']}, Para1 {row['native_para1']}, Para2 {row['native_para2']}, Para3 {row['native_para3']}, Para4 {row['native_para4']})"
            )
        (repo / REPORT_OUTPUT).parent.mkdir(parents=True, exist_ok=True)
        (repo / REPORT_OUTPUT).write_text("\n".join(report) + "\n", encoding="utf-8")
    return evidence


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=ROOT)
    parser.add_argument("--source", type=Path, default=None)
    parser.add_argument("--check", action="store_true", help="Validate and report without modifying Librarium records")
    args = parser.parse_args()
    repo = args.repo.resolve()
    source = (args.source or (repo / DEFAULT_SOURCE)).resolve()
    if not source.is_file():
        raise SystemExit(f"WCCMP002 source file not found: {source}")
    result = recover(repo, source, apply=not args.check)
    print(
        f"WCCMP002 native exit recovery PASS: {result['validated_room_record_count']} room offsets, "
        f"{result['canonical_exit_count']} exits checked, {result['trap_count']} exact trap states projected; "
        f"Para2 distribution {result['trap_para2_distribution']}"
    )


if __name__ == "__main__":
    main()
