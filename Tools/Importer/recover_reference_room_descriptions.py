#!/usr/bin/env python3
"""Recover map-qualified room descriptions from approved local references.

Native map DAT room text remains the highest-priority evidence. This tool only
fills room-description gaps when a reference source provides an explicit
Classic map id and room number that matches the active Librarium room catalog.
"""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


CODE_UPDATE = "0139"
DATABASE_VERSION = "0010"
OMNI_ROOM_ROOT = Path(
    "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/rooms"
)
OMNI_SUMMARY = Path(
    "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/"
    "room-capture-summary-wcc.json"
)
MMX_ROOM_FILE = Path("Librarium/Objects/rooms/mmexplorer_room_records_0140.json")
NATIVE_DESCRIPTION_FILE = Path(
    "Librarium/Objects/room_descriptions/native_map_room_description_records_0139.json"
)
OUTPUT_DESCRIPTION_FILE = Path(
    "Librarium/Objects/room_descriptions/reference_room_description_records_0139.json"
)
REPORT_JSON = Path("Reports/DataDecode/room_description_reference_recovery_0139.json")
REPORT_MD = Path("Reports/DataDecode/room_description_reference_recovery_0139.md")


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def room_records(root: Any) -> list[dict[str, Any]]:
    if isinstance(root, dict) and isinstance(root.get("records"), list):
        return [row for row in root["records"] if isinstance(row, dict)]
    if isinstance(root, list):
        return [row for row in root if isinstance(row, dict)]
    return []


def read_int(row: dict[str, Any], *keys: str) -> int | None:
    for key in keys:
        value = row.get(key)
        if isinstance(value, int):
            return value
        if isinstance(value, str) and value.strip().isdigit():
            return int(value.strip())
    return None


def normalize_name(value: Any) -> str:
    return " ".join(str(value or "").strip().lower().split())


def omni_description(row: dict[str, Any]) -> str:
    lines: list[str] = []
    for index in range(1, 8):
        text = str(row.get(f"desc{index}") or "").strip()
        if text:
            lines.append(text)
    return "\n".join(lines).strip()


def recover(repo: Path) -> dict[str, Any]:
    rooms = room_records(load_json(repo / MMX_ROOM_FILE))
    canonical_by_map_room: dict[tuple[int, int], dict[str, Any]] = {}
    for room in rooms:
        map_id = read_int(room, "map_number", "map_id")
        room_number = read_int(room, "room_number")
        if map_id is None or room_number is None:
            continue
        canonical_by_map_room[(map_id, room_number)] = room

    native_records = room_records(load_json(repo / NATIVE_DESCRIPTION_FILE))
    native_room_ids = {
        str(row.get("room_id"))
        for row in native_records
        if row.get("room_id") is not None
    }

    omni_files = sorted((repo / OMNI_ROOM_ROOT).glob("*.json"))
    records: list[dict[str, Any]] = []
    skipped_name_mismatch: list[dict[str, Any]] = []
    skipped_missing_canonical: list[dict[str, Any]] = []
    skipped_native_existing = 0
    empty_descriptions = 0

    for path in omni_files:
        row = load_json(path)
        if not isinstance(row, dict):
            continue
        map_id = read_int(row, "map_id")
        room_number = read_int(row, "room_number")
        description = omni_description(row)
        if not description:
            empty_descriptions += 1
            continue
        if map_id is None or room_number is None:
            skipped_missing_canonical.append({"file": str(path), "reason": "missing map_id/room_number"})
            continue
        canonical = canonical_by_map_room.get((map_id, room_number))
        if canonical is None:
            skipped_missing_canonical.append(
                {
                    "file": str(path.relative_to(repo)).replace("\\", "/"),
                    "map_number": map_id,
                    "room_number": room_number,
                    "name": row.get("name"),
                }
            )
            continue
        room_id = str(canonical.get("id") or "")
        if room_id in native_room_ids:
            skipped_native_existing += 1
            continue
        if normalize_name(canonical.get("name")) != normalize_name(row.get("name")):
            skipped_name_mismatch.append(
                {
                    "file": str(path.relative_to(repo)).replace("\\", "/"),
                    "room_id": room_id,
                    "map_number": map_id,
                    "room_number": room_number,
                    "librarium_name": canonical.get("name"),
                    "reference_name": row.get("name"),
                }
            )
            continue

        relative = str(path.relative_to(repo)).replace("\\", "/")
        records.append(
            {
                "id": f"room_description.{room_id}",
                "room_id": room_id,
                "legacy_id": canonical.get("legacy_id"),
                "map_number": map_id,
                "room_number": room_number,
                "name": canonical.get("name"),
                "object_type": "room_description",
                "world_scope": ["classic"],
                "description": description,
                "evidence_refs": [
                    {
                        "source": "omniverse_room_detail_snapshot",
                        "source_file": relative,
                        "source_kind": "HTTrack OmniVerse public codex room detail JSON",
                        "map_id": map_id,
                        "room_number": room_number,
                        "reference_name": row.get("name"),
                        "description_line_fields": [
                            f"desc{index}"
                            for index in range(1, 8)
                            if str(row.get(f"desc{index}") or "").strip()
                        ],
                        "classification": "map_qualified_reference_room_description_gap_fill",
                        "promotion_code_update": CODE_UPDATE,
                        "override_policy": "fills_only_rooms_without_native_binary_backed_description",
                    }
                ],
                "classic_lock": "reference_backed_needs_binary_confirmation",
                "runtime_mapping": {
                    "source": "Librarium/Objects/room_descriptions",
                    "generated_only": True,
                    "runtime_reads_generated_data": True,
                    "status": "reference_gap_fill_room_description",
                    "exports_to": "/Data/<world>/rooms.json",
                },
            }
        )

    records.sort(key=lambda row: (int(row["map_number"]), int(row["room_number"])))
    output = {
        "schema": "librarium_room_description_records_v1",
        "code_update": CODE_UPDATE,
        "database_version": DATABASE_VERSION,
        "source_of_truth": "map_qualified_reference_gap_fill",
        "priority": "lower_than_native_binary_room_descriptions",
        "record_count": len(records),
        "records": records,
    }
    write_json(repo / OUTPUT_DESCRIPTION_FILE, output)

    summary = {
        "generatedUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
        "codeUpdate": CODE_UPDATE,
        "databaseVersion": DATABASE_VERSION,
        "canonicalRoomRecords": len(rooms),
        "nativeBinaryDescriptionRecords": len(native_records),
        "omniRoomDetailFiles": len(omni_files),
        "omniEmptyDescriptions": empty_descriptions,
        "referenceDescriptionsPromoted": len(records),
        "skippedAlreadyNativeBacked": skipped_native_existing,
        "skippedMissingCanonical": len(skipped_missing_canonical),
        "skippedNameMismatch": len(skipped_name_mismatch),
        "output": str(OUTPUT_DESCRIPTION_FILE).replace("\\", "/"),
        "sourceSummary": str(OMNI_SUMMARY).replace("\\", "/"),
        "firstMissingCanonical": skipped_missing_canonical[:10],
        "firstNameMismatches": skipped_name_mismatch[:10],
    }
    write_json(repo / REPORT_JSON, summary)
    (repo / REPORT_MD).parent.mkdir(parents=True, exist_ok=True)
    (repo / REPORT_MD).write_text(
        "\n".join(
            [
                "# Room Description Reference Recovery 0139",
                "",
                "## Result",
                "",
                f"- Canonical room records checked: `{len(rooms)}`",
                f"- Native binary-backed descriptions already present: `{len(native_records)}`",
                f"- OmniVerse room detail JSON files checked: `{len(omni_files)}`",
                f"- Reference descriptions promoted: `{len(records)}`",
                f"- Skipped because native binary evidence already exists: `{skipped_native_existing}`",
                f"- Skipped due to missing canonical room identity: `{len(skipped_missing_canonical)}`",
                f"- Skipped due to name mismatch: `{len(skipped_name_mismatch)}`",
                "",
                "## Evidence Policy",
                "",
                "Native map DAT room descriptions remain higher priority. This pass only fills",
                "rooms that have no native binary-backed description and whose OmniVerse room",
                "detail file carries an explicit `map_id` and `room_number` matching the",
                "active Librarium room catalog. These records are marked",
                "`reference_backed_needs_binary_confirmation`.",
                "",
                "## Output",
                "",
                f"- `{OUTPUT_DESCRIPTION_FILE.as_posix()}`",
                f"- `{REPORT_JSON.as_posix()}`",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    return summary


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".", help="Repository root")
    args = parser.parse_args()
    summary = recover(Path(args.repo).resolve())
    print(
        "referenceDescriptionsPromoted="
        f"{summary['referenceDescriptionsPromoted']} "
        f"skippedAlreadyNativeBacked={summary['skippedAlreadyNativeBacked']} "
        f"skippedMissingCanonical={summary['skippedMissingCanonical']} "
        f"skippedNameMismatch={summary['skippedNameMismatch']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
