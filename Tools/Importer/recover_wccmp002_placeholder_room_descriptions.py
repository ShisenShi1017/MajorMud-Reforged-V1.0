#!/usr/bin/env python3
"""Recover placeholder-named room descriptions from WCCMP002 native offsets.

The earlier WCCMP002 recovery pass matched room titles. That intentionally left
placeholder catalog names such as "Room 15/850" unresolved because the binary
contains the real title instead of the placeholder. This pass uses the already
decoded WCCMP002 native room-record offset on each room to recover the title and
description from the room's own native text block.
"""
from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROMOTION_SCOPE = "wccmp002_placeholder_room_description_recovery"
DATABASE_VERSION = "0010"
ROOMS_FILE = Path("Librarium/Objects/rooms/mmexplorer_room_records_0140.json")
WCC_SOURCE_FILE = Path("ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat")
WBB_REFERENCE_FILE = Path("ImportedDatabases/WCCLabeledWBB/Extracted/WBBMP002.DAT")
OUTPUT_FILE = Path("Librarium/Objects/room_descriptions/wccmp002_placeholder_room_description_records.json")
REPORT_JSON = Path("Reports/Generated/wccmp002_placeholder_room_description_recovery.json")
REPORT_MD = Path("Reports/Generated/wccmp002_placeholder_room_description_recovery.md")
KNOWN_DESCRIPTION_GLOB = Path("Librarium/Objects/room_descriptions")
TITLE_SEARCH_BACK_BYTES = 4096
DESCRIPTION_OFFSETS = (53, 124, 195, 266, 337, 408, 479)
TITLE_OFFSET_MODS = {269, 1815}


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


def known_room_ids(repo: Path) -> set[str]:
    known: set[str] = set()
    root = repo / KNOWN_DESCRIPTION_GLOB
    if not root.is_dir():
        return known
    output_path = repo / OUTPUT_FILE
    for path in root.glob("*_records_*.json"):
        if path.resolve() == output_path.resolve():
            continue
        for record in room_records(load_json(path)):
            room_id = str(record.get("room_id") or "")
            description = str(record.get("description") or "").strip()
            if room_id and description:
                known.add(room_id)
    return known


def normalize(value: str) -> str:
    return re.sub(r"\s+", " ", value.strip().lower())


def read_ascii(data: bytes, start: int, limit: int = 90) -> str:
    if start < 0 or start >= len(data):
        return ""
    end = start
    while end < len(data) and 32 <= data[end] <= 126 and end - start <= limit:
        end += 1
    return data[start:end].decode("latin1", "replace").strip()


def printable_strings(data: bytes, start: int, end: int) -> list[tuple[int, str]]:
    results: list[tuple[int, str]] = []
    index = max(0, start)
    end = min(len(data), end)
    while index < end:
        if 32 <= data[index] <= 126:
            string_start = index
            while index < end and 32 <= data[index] <= 126:
                index += 1
            if index - string_start >= 4:
                text = data[string_start:index].decode("latin1", "replace").strip()
                if text:
                    results.append((string_start, text))
            continue
        index += 1
    return results


def is_title_candidate(text: str) -> bool:
    if len(text) < 3 or len(text) > 80:
        return False
    upper = text.upper()
    if upper.endswith(".ANS") or upper.endswith(".HSE"):
        return False
    if upper in {"FILE DESCRIPTION", "COMMANDS:"}:
        return False
    if text.startswith("Room "):
        return False
    return any(ch.isalpha() for ch in text)


def is_description_line(text: str) -> bool:
    if len(text) < 8:
        return False
    upper = text.upper()
    if upper.endswith(".ANS") or upper.endswith(".HSE"):
        return False
    if upper in {"FILE DESCRIPTION", "COMMANDS:"}:
        return False
    return any(ch.isalpha() for ch in text)


def extract_description(data: bytes, title_offset: int) -> str:
    lines: list[str] = []
    for delta in DESCRIPTION_OFFSETS:
        line = read_ascii(data, title_offset + delta)
        if not is_description_line(line):
            continue
        lines.append(line)
    return "\n".join(lines).strip()


def score_candidate(title: str, description: str, native_offset: int, title_offset: int) -> tuple[int, int, int]:
    line_count = len([line for line in description.splitlines() if line.strip()])
    # The decoded native_record_offset belongs to the room being recovered. The
    # nearest preceding WCCMP002 title block is the strongest local evidence.
    distance_score = -abs(native_offset - title_offset)
    shape_score = line_count * 10
    if " " in title:
        shape_score += 2
    if len(title) > 8:
        shape_score += 1
    return distance_score, shape_score, line_count


def recover_from_native_offset(data: bytes, native_offset: int) -> dict[str, Any] | None:
    start = max(0, native_offset - TITLE_SEARCH_BACK_BYTES)
    candidates: list[dict[str, Any]] = []
    for offset, title in printable_strings(data, start, native_offset + 1):
        if offset % 2048 not in TITLE_OFFSET_MODS:
            continue
        if not is_title_candidate(title):
            continue
        description = extract_description(data, offset)
        if len(description) < 30:
            continue
        score = score_candidate(title, description, native_offset, offset)
        candidates.append(
            {
                "title": title,
                "description": description,
                "title_offset": offset,
                "title_offset_mod_2048": offset % 2048,
                "native_record_offset": native_offset,
                "score": score,
            }
        )
    if not candidates:
        return None
    candidates.sort(key=lambda row: row["score"], reverse=True)
    selected = candidates[0]
    selected["candidate_count"] = len(candidates)
    selected["candidate_titles"] = [row["title"] for row in candidates[:8]]
    return selected


def wbb_corroboration(wbb_data: bytes, title: str, description: str) -> dict[str, Any]:
    title_hits = find_all(wbb_data, title.encode("latin1", "ignore"))
    first_line = description.splitlines()[0] if description.splitlines() else ""
    first_line_hits = find_all(wbb_data, first_line.encode("latin1", "ignore")) if first_line else []
    return {
        "source_file": str(WBB_REFERENCE_FILE).replace("\\", "/"),
        "title_hit_count": len(title_hits),
        "title_offsets_sample": title_hits[:10],
        "first_description_line_hit_count": len(first_line_hits),
        "first_description_line_offsets_sample": first_line_hits[:10],
    }


def find_all(data: bytes, needle: bytes) -> list[int]:
    if not needle:
        return []
    hits: list[int] = []
    start = 0
    while True:
        index = data.find(needle, start)
        if index < 0:
            return hits
        hits.append(index)
        start = index + 1


def recover(repo: Path) -> dict[str, Any]:
    rooms = room_records(load_json(repo / ROOMS_FILE))
    known = known_room_ids(repo)
    wcc_data = (repo / WCC_SOURCE_FILE).read_bytes()
    wbb_data = (repo / WBB_REFERENCE_FILE).read_bytes() if (repo / WBB_REFERENCE_FILE).is_file() else b""

    records: list[dict[str, Any]] = []
    unresolved: list[dict[str, Any]] = []
    placeholder_candidates = 0
    wbb_title_correlated = 0
    wbb_line_correlated = 0

    for room in sorted(rooms, key=lambda row: (int(row["map_number"]), int(row["room_number"]))):
        room_id = str(room.get("id") or "")
        if room_id in known:
            continue
        name = str(room.get("name") or "")
        if not re.fullmatch(r"Room \d+/\d+", name):
            continue
        placeholder_candidates += 1
        placed = room.get("placed_definition") if isinstance(room.get("placed_definition"), dict) else {}
        native_offset = placed.get("native_record_offset")
        if not isinstance(native_offset, int):
            unresolved.append({"room_id": room_id, "name": name, "reason": "missing native_record_offset"})
            continue
        recovered = recover_from_native_offset(wcc_data, native_offset)
        if recovered is None:
            unresolved.append(
                {
                    "room_id": room_id,
                    "name": name,
                    "native_record_offset": native_offset,
                    "reason": "no recoverable title/description near native offset",
                }
            )
            continue

        wbb = wbb_corroboration(wbb_data, recovered["title"], recovered["description"]) if wbb_data else {}
        if wbb.get("title_hit_count", 0) > 0:
            wbb_title_correlated += 1
        if wbb.get("first_description_line_hit_count", 0) > 0:
            wbb_line_correlated += 1

        records.append(
            {
                "id": f"room_description.{room_id}",
                "room_id": room_id,
                "legacy_id": room.get("legacy_id"),
                "map_number": int(room["map_number"]),
                "room_number": int(room["room_number"]),
                "name": recovered["title"],
                "catalog_placeholder_name": name,
                "object_type": "room_description",
                "world_scope": ["classic"],
                "description": recovered["description"],
                "evidence_refs": [
                    {
                        "source": "wccmp002_native_room_record_offset",
                        "source_file": str(WCC_SOURCE_FILE).replace("\\", "/"),
                        "source_kind": "WCCMP002 MajorMUD 1.11p map binary",
                        "native_record_offset": native_offset,
                        "title_offset": recovered["title_offset"],
                        "title_offset_mod_2048": recovered["title_offset_mod_2048"],
                        "description_offsets_from_title": list(DESCRIPTION_OFFSETS),
                        "candidate_count": recovered["candidate_count"],
                        "candidate_titles": recovered["candidate_titles"],
                        "classification": "placeholder_name_native_offset_room_description",
                        "promotion_scope": PROMOTION_SCOPE,
                        "validation_policy": (
                            "Accepted because the placeholder catalog room has a decoded WCCMP002 "
                            "native_record_offset and the nearest native text block yields a "
                            "non-empty title and multi-line description."
                        ),
                    },
                    {
                        "source": "wbbmp002_secondary_corroboration",
                        "source_kind": "WBBMP002 secondary comparison; WCCMP002 remains the selected 1.11p source",
                        **wbb,
                    },
                ],
                "classic_lock": "wccmp002_1_11p_binary_backed_placeholder_recovery",
                "runtime_mapping": {
                    "source": "Librarium/Objects/room_descriptions",
                    "generated_only": True,
                    "runtime_reads_generated_data": True,
                    "status": "wccmp002_binary_gap_fill_room_description",
                    "exports_to": "/Data/<world>/rooms.json",
                },
            }
        )

    output = {
        "schema": "librarium_room_description_records_v1",
        "promotion_scope": PROMOTION_SCOPE,
        "database_version": DATABASE_VERSION,
        "source_of_truth": "wccmp002_1_11p_native_record_offset_gap_fill",
        "priority": "same_as_wccmp002_binary_gap_fill_higher_than_public_reference",
        "record_count": len(records),
        "records": records,
    }
    write_json(repo / OUTPUT_FILE, output)

    summary = {
        "generatedUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
        "promotionScope": PROMOTION_SCOPE,
        "databaseVersion": DATABASE_VERSION,
        "canonicalRoomRecords": len(rooms),
        "knownDescriptionsBeforeRecovery": len(known),
        "placeholderCandidatesWithoutDescriptions": placeholder_candidates,
        "recordsPromoted": len(records),
        "unresolvedAfterRecovery": len(unresolved),
        "totalDescriptionsAfterRecovery": len(known) + len(records),
        "wbbTitleCorrelated": wbb_title_correlated,
        "wbbFirstDescriptionLineCorrelated": wbb_line_correlated,
        "source": str(WCC_SOURCE_FILE).replace("\\", "/"),
        "secondaryComparison": str(WBB_REFERENCE_FILE).replace("\\", "/"),
        "output": str(OUTPUT_FILE).replace("\\", "/"),
        "unresolvedRooms": unresolved,
        "sampleRecovered": [
            {
                "room_id": record["room_id"],
                "catalog_placeholder_name": record["catalog_placeholder_name"],
                "recovered_title": record["name"],
                "first_description_line": record["description"].splitlines()[0],
            }
            for record in records[:15]
        ],
    }
    write_json(repo / REPORT_JSON, summary)
    (repo / REPORT_MD).parent.mkdir(parents=True, exist_ok=True)
    (repo / REPORT_MD).write_text(
        "\n".join(
            [
                "# WCCMP002 Placeholder Room Description Recovery",
                "",
                "## Result",
                "",
                f"- Canonical room records checked: `{len(rooms)}`",
                f"- Existing room descriptions before recovery: `{len(known)}`",
                f"- Placeholder rooms without descriptions checked: `{placeholder_candidates}`",
                f"- New WCCMP002 native-offset descriptions promoted: `{len(records)}`",
                f"- Still unresolved after this pass: `{len(unresolved)}`",
                f"- Total descriptions after recovery: `{len(known) + len(records)}`",
                f"- WBB title corroboration hits: `{wbb_title_correlated}`",
                f"- WBB first-line corroboration hits: `{wbb_line_correlated}`",
                "",
                "## Evidence Policy",
                "",
                "`WCCMP002.DAT` is treated as the selected MajorMUD 1.11p room/map",
                "source for this pass. `WBBMP002.DAT` is used only as secondary",
                "comparison evidence. Records are emitted only for placeholder-named",
                "rooms that lacked an existing Librarium description and already had a",
                "decoded WCCMP002 native room-record offset.",
                "",
                "## Output",
                "",
                f"- `{OUTPUT_FILE.as_posix()}`",
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
        f"recordsPromoted={summary['recordsPromoted']} "
        f"unresolvedAfterRecovery={summary['unresolvedAfterRecovery']} "
        f"totalDescriptionsAfterRecovery={summary['totalDescriptionsAfterRecovery']} "
        f"wbbTitleCorrelated={summary['wbbTitleCorrelated']} "
        f"wbbFirstDescriptionLineCorrelated={summary['wbbFirstDescriptionLineCorrelated']}"
    )
    return 0 if summary["unresolvedAfterRecovery"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
