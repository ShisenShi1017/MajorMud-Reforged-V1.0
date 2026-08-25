#!/usr/bin/env python3
"""Recover room descriptions from the local WCC default map binary.

The default-install `wccmp002.dat` contains map-qualified room prose that is not
present in the clean `NativeRuntime/WCCMP001.DAT` slice. Its string area has
interleaved bleed-through fragments, so this recovery pass is intentionally
conservative:

- only room-title occurrences preceded by a long zero run are considered;
- only canonical room titles from Librarium are accepted;
- duplicate-name groups are accepted only when their already-known rooms
  validate against existing Librarium descriptions in file order;
- existing native/reference descriptions are never overwritten.
"""
from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROMOTION_SCOPE = "wccmp002_room_description_recovery"
DATABASE_VERSION = "0010"
ROOMS_FILE = Path("Librarium/Objects/rooms/mmexplorer_room_records_0140.json")
SOURCE_FILE = Path("ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat")
OUTPUT_FILE = Path("Librarium/Objects/room_descriptions/wccmp002_room_description_records.json")
REPORT_JSON = Path("Reports/Generated/wccmp002_room_description_recovery.json")
REPORT_MD = Path("Reports/Generated/wccmp002_room_description_recovery.md")
KNOWN_DESCRIPTION_FILES = (
    Path("Librarium/Objects/room_descriptions/native_map_room_description_records.json"),
    Path("Librarium/Objects/room_descriptions/reference_room_description_records.json"),
)
TITLE_MODS = {269, 1815}
DESCRIPTION_SLOTS = (118, 189, 260, 331, 402, 473, 545)


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def normalize(value: str) -> str:
    return re.sub(r"\s+", " ", value.strip().lower())


def room_records(root: Any) -> list[dict[str, Any]]:
    if isinstance(root, dict) and isinstance(root.get("records"), list):
        return [row for row in root["records"] if isinstance(row, dict)]
    if isinstance(root, list):
        return [row for row in root if isinstance(row, dict)]
    return []


def known_descriptions(repo: Path) -> dict[str, str]:
    known: dict[str, str] = {}
    for relative in KNOWN_DESCRIPTION_FILES:
        path = repo / relative
        if not path.exists():
            continue
        for record in room_records(load_json(path)):
            room_id = str(record.get("room_id") or "")
            description = str(record.get("description") or "").strip()
            if room_id and description:
                known[room_id] = description
    return known


def scan_title_occurrences(data: bytes, canonical_names: set[str]) -> dict[str, list[int]]:
    occurrences: dict[str, list[int]] = defaultdict(list)
    index = 0
    zero_run = 0
    while index < len(data):
        byte = data[index]
        if byte == 0:
            zero_run += 1
            index += 1
            continue

        if 32 <= byte <= 126 and zero_run >= 64:
            end = index
            while end < len(data) and 32 <= data[end] <= 126 and end - index <= 120:
                end += 1
            title = data[index:end].decode("latin1", "replace").strip()
            if title in canonical_names and index % 2048 in TITLE_MODS:
                occurrences[title].append(index)
            zero_run = 0
            index = end
            continue

        zero_run = 0
        index += 1

    return {name: offsets for name, offsets in occurrences.items()}


def read_slot(data: bytes, block_start: int, slot: int) -> str:
    position = block_start + slot
    start = position
    while start < min(position + 6, len(data)) and not 32 <= data[start] <= 126:
        start += 1
    if start >= len(data) or not 32 <= data[start] <= 126:
        return ""

    end = start
    while end < len(data) and 32 <= data[end] <= 126 and end - start <= 90:
        end += 1
    return data[start:end].decode("latin1", "replace").strip()


def extract_description(data: bytes, title_offset: int) -> str:
    block_start = title_offset - 65
    lines: list[str] = []
    for slot in DESCRIPTION_SLOTS:
        line = read_slot(data, block_start, slot)
        if not line:
            continue
        if line.upper().endswith(".ANS"):
            continue
        if len(line) < 4:
            continue
        if lines and len(line) < 35 and line.lower() != "commands:":
            break
        lines.append(line)
    return "\n".join(lines).strip()


def first_line_matches(candidate: str, expected: str) -> bool:
    if not candidate or not expected:
        return False
    return normalize(candidate.splitlines()[0]) == normalize(expected.splitlines()[0])


def recover(repo: Path) -> dict[str, Any]:
    rooms = room_records(load_json(repo / ROOMS_FILE))
    known = known_descriptions(repo)
    source_path = repo / SOURCE_FILE
    data = source_path.read_bytes()

    by_name: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for room in rooms:
        name = str(room.get("name") or "").strip()
        if name:
            by_name[name].append(room)

    occurrences = scan_title_occurrences(data, set(by_name))
    records: list[dict[str, Any]] = []
    accepted_groups = 0
    rejected_groups = 0
    duplicate_group_records = 0
    unique_group_records = 0
    identical_title_group_records = 0
    user_rule_duplicate_title_records = 0
    skipped_existing = 0
    skipped_empty = 0
    validation_checks = 0
    validation_passes = 0
    first_rejected_groups: list[dict[str, Any]] = []
    promoted_room_ids: set[str] = set()

    for name, name_rooms in by_name.items():
        offsets = occurrences.get(name, [])
        if len(offsets) != len(name_rooms):
            if offsets and len(first_rejected_groups) < 25:
                first_rejected_groups.append(
                    {
                        "name": name,
                        "canonical_room_count": len(name_rooms),
                        "source_title_occurrence_count": len(offsets),
                        "reason": "title occurrence count did not match canonical room count",
                    }
                )
            rejected_groups += 1
            continue

        paired = list(
            zip(
                sorted(name_rooms, key=lambda row: (int(row["map_number"]), int(row["room_number"]))),
                sorted(offsets),
            )
        )
        group_valid = True
        for room, offset in paired:
            room_id = str(room.get("id") or "")
            if room_id not in known:
                continue
            validation_checks += 1
            candidate = extract_description(data, offset)
            if first_line_matches(candidate, known[room_id]):
                validation_passes += 1
            else:
                group_valid = False
                break

        if len(paired) > 1 and not group_valid:
            rejected_groups += 1
            if len(first_rejected_groups) < 25:
                first_rejected_groups.append(
                    {
                        "name": name,
                        "canonical_room_count": len(name_rooms),
                        "source_title_occurrence_count": len(offsets),
                        "reason": "duplicate-name group failed known-description validation",
                    }
                )
            continue

        accepted_groups += 1
        for room, offset in paired:
            room_id = str(room.get("id") or "")
            if room_id in known:
                skipped_existing += 1
                continue
            description = extract_description(data, offset)
            if len(description) < 30:
                skipped_empty += 1
                continue

            classification = (
                "unique_title_binary_room_description"
                if len(paired) == 1
                else "duplicate_title_file_order_validated_binary_room_description"
            )
            if len(paired) == 1:
                unique_group_records += 1
            else:
                duplicate_group_records += 1

            records.append(
                {
                    "id": f"room_description.{room_id}",
                    "room_id": room_id,
                    "legacy_id": room.get("legacy_id"),
                    "map_number": int(room["map_number"]),
                    "room_number": int(room["room_number"]),
                    "name": room.get("name"),
                    "object_type": "room_description",
                    "world_scope": ["classic"],
                    "description": description,
                    "evidence_refs": [
                        {
                            "source": "wcc_default_map_binary",
                            "source_file": str(SOURCE_FILE).replace("\\", "/"),
                            "source_kind": "WCC default install map binary",
                            "title_offset": offset,
                            "title_offset_mod_2048": offset % 2048,
                            "description_slots": list(DESCRIPTION_SLOTS),
                            "classification": classification,
                            "promotion_scope": PROMOTION_SCOPE,
                            "validation_policy": (
                                "Accepted because the title was unique, or because duplicate-name "
                                "file order matched all already-known descriptions in the group."
                            ),
                            "override_policy": "fills_only_rooms_without_existing_librarium_description",
                        }
                    ],
                    "classic_lock": "binary_backed_default_wcc_needs_native_runtime_confirmation",
                    "runtime_mapping": {
                        "source": "Librarium/Objects/room_descriptions",
                        "generated_only": True,
                        "runtime_reads_generated_data": True,
                        "status": "wccmp002_binary_gap_fill_room_description",
                        "exports_to": "/Data/<world>/rooms.json",
                    },
                }
            )
            promoted_room_ids.add(room_id)

    for name, name_rooms in by_name.items():
        remaining_rooms = [
            room
            for room in name_rooms
            if str(room.get("id") or "") not in known
            and str(room.get("id") or "") not in promoted_room_ids
        ]
        if not remaining_rooms:
            continue

        offsets = occurrences.get(name, [])
        extracted_by_text: dict[str, tuple[str, list[int]]] = {}
        for offset in offsets:
            description = extract_description(data, offset)
            if len(description) < 30:
                continue
            key = normalize(description)
            if key not in extracted_by_text:
                extracted_by_text[key] = (description, [])
            extracted_by_text[key][1].append(offset)

        if len(extracted_by_text) != 1:
            continue

        description, source_offsets = next(iter(extracted_by_text.values()))
        for room in sorted(remaining_rooms, key=lambda row: (int(row["map_number"]), int(row["room_number"]))):
            room_id = str(room.get("id") or "")
            records.append(
                {
                    "id": f"room_description.{room_id}",
                    "room_id": room_id,
                    "legacy_id": room.get("legacy_id"),
                    "map_number": int(room["map_number"]),
                    "room_number": int(room["room_number"]),
                    "name": room.get("name"),
                    "object_type": "room_description",
                    "world_scope": ["classic"],
                    "description": description,
                    "evidence_refs": [
                        {
                            "source": "wcc_default_map_binary",
                            "source_file": str(SOURCE_FILE).replace("\\", "/"),
                            "source_kind": "WCC default install map binary",
                            "title_offsets_sample": source_offsets[:10],
                            "title_offset_count": len(source_offsets),
                            "title_offset_mod_2048_values": sorted({offset % 2048 for offset in source_offsets}),
                            "description_slots": list(DESCRIPTION_SLOTS),
                            "classification": "duplicate_title_identical_description_binary_room_description",
                            "promotion_scope": PROMOTION_SCOPE,
                            "validation_policy": (
                                "Accepted because every extractable WCCMP002 candidate for this unresolved "
                                "duplicate title had the same normalized description."
                            ),
                            "override_policy": "fills_only_rooms_without_existing_librarium_description",
                        }
                    ],
                    "classic_lock": "binary_backed_default_wcc_needs_native_runtime_confirmation",
                    "runtime_mapping": {
                        "source": "Librarium/Objects/room_descriptions",
                        "generated_only": True,
                        "runtime_reads_generated_data": True,
                        "status": "wccmp002_binary_gap_fill_room_description",
                        "exports_to": "/Data/<world>/rooms.json",
                    },
                }
            )
            promoted_room_ids.add(room_id)
            identical_title_group_records += 1

    for name, name_rooms in by_name.items():
        remaining_rooms = [
            room
            for room in name_rooms
            if str(room.get("id") or "") not in known
            and str(room.get("id") or "") not in promoted_room_ids
        ]
        if not remaining_rooms:
            continue

        candidates: list[tuple[str, int]] = []
        for offset in occurrences.get(name, []):
            description = extract_description(data, offset)
            if len(description) >= 30:
                candidates.append((description, offset))
        if not candidates:
            continue

        counts = Counter(normalize(description) for description, _ in candidates)
        selected_key, selected_count = counts.most_common(1)[0]
        selected_description = next(description for description, _ in candidates if normalize(description) == selected_key)
        selected_offsets = [offset for description, offset in candidates if normalize(description) == selected_key]

        for room in sorted(remaining_rooms, key=lambda row: (int(row["map_number"]), int(row["room_number"]))):
            room_id = str(room.get("id") or "")
            records.append(
                {
                    "id": f"room_description.{room_id}",
                    "room_id": room_id,
                    "legacy_id": room.get("legacy_id"),
                    "map_number": int(room["map_number"]),
                    "room_number": int(room["room_number"]),
                    "name": room.get("name"),
                    "object_type": "room_description",
                    "world_scope": ["classic"],
                    "description": selected_description,
                    "evidence_refs": [
                        {
                            "source": "wcc_default_map_binary",
                            "source_file": str(SOURCE_FILE).replace("\\", "/"),
                            "source_kind": "WCC default install map binary",
                            "title_offsets_sample": selected_offsets[:10],
                            "title_offset_count": len(selected_offsets),
                            "candidate_description_count": len(candidates),
                            "distinct_candidate_description_count": len(counts),
                            "selected_description_candidate_count": selected_count,
                            "description_slots": list(DESCRIPTION_SLOTS),
                            "classification": "duplicate_title_user_rule_binary_room_description",
                            "promotion_scope": PROMOTION_SCOPE,
                            "validation_policy": (
                                "Accepted under the user-approved rule that duplicate rooms share one "
                                "description. The selected description is the most common normalized "
                                "WCCMP002 extraction for this room title."
                            ),
                            "override_policy": "fills_only_rooms_without_existing_librarium_description",
                        }
                    ],
                    "classic_lock": "binary_backed_default_wcc_duplicate_title_user_rule",
                    "runtime_mapping": {
                        "source": "Librarium/Objects/room_descriptions",
                        "generated_only": True,
                        "runtime_reads_generated_data": True,
                        "status": "wccmp002_binary_gap_fill_room_description",
                        "exports_to": "/Data/<world>/rooms.json",
                    },
                }
            )
            promoted_room_ids.add(room_id)
            user_rule_duplicate_title_records += 1

    records.sort(key=lambda row: (int(row["map_number"]), int(row["room_number"])))
    recovered_room_ids = {str(record["room_id"]) for record in records}
    unresolved_rooms = [
        {
            "room_id": str(room.get("id") or ""),
            "map_number": int(room["map_number"]),
            "room_number": int(room["room_number"]),
            "name": room.get("name"),
            "relationships": room.get("relationships", {}),
            "source_fields": room.get("source_fields", {}),
            "classification": "placeholder_named_room_without_local_wcc_description_evidence",
        }
        for room in sorted(rooms, key=lambda row: (int(row["map_number"]), int(row["room_number"])))
        if str(room.get("id") or "") not in known
        and str(room.get("id") or "") not in recovered_room_ids
    ]
    write_json(
        repo / OUTPUT_FILE,
        {
            "schema": "librarium_room_description_records_v1",
            "promotion_scope": PROMOTION_SCOPE,
            "database_version": DATABASE_VERSION,
            "source_of_truth": "wcc_default_map_binary_gap_fill",
            "priority": "lower_than_native_runtime_binary_descriptions_higher_than_public_reference",
            "record_count": len(records),
            "records": records,
        },
    )

    name_counts = Counter(room.get("name") for room in rooms if room.get("name"))
    summary = {
        "generatedUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
        "promotionScope": PROMOTION_SCOPE,
        "databaseVersion": DATABASE_VERSION,
        "canonicalRoomRecords": len(rooms),
        "knownDescriptionsBeforeRecovery": len(known),
        "canonicalRoomNames": len(by_name),
        "sourceTitleOccurrences": sum(len(value) for value in occurrences.values()),
        "sourceCanonicalNamesWithOccurrences": len(occurrences),
        "uniqueCanonicalRoomNames": sum(1 for count in name_counts.values() if count == 1),
        "acceptedGroups": accepted_groups,
        "rejectedGroups": rejected_groups,
        "validationChecks": validation_checks,
        "validationPasses": validation_passes,
        "recordsPromoted": len(records),
        "recordsFromUniqueTitleGroups": unique_group_records,
        "recordsFromValidatedDuplicateTitleGroups": duplicate_group_records,
        "recordsFromIdenticalDuplicateTitleGroups": identical_title_group_records,
        "recordsFromUserRuleDuplicateTitleGroups": user_rule_duplicate_title_records,
        "totalDescriptionsAfterRecovery": len(known) + len(records),
        "unresolvedAfterRecovery": len(unresolved_rooms),
        "unresolvedRooms": unresolved_rooms,
        "skippedExistingDescriptions": skipped_existing,
        "skippedEmptyOrTooShortDescriptions": skipped_empty,
        "output": str(OUTPUT_FILE).replace("\\", "/"),
        "source": str(SOURCE_FILE).replace("\\", "/"),
        "firstRejectedGroups": first_rejected_groups,
    }
    write_json(repo / REPORT_JSON, summary)
    (repo / REPORT_MD).parent.mkdir(parents=True, exist_ok=True)
    (repo / REPORT_MD).write_text(
        "\n".join(
            [
                "# WCCMP002 Room Description Recovery",
                "",
                "## Result",
                "",
                f"- Canonical room records checked: `{len(rooms)}`",
                f"- Existing descriptions before recovery: `{len(known)}`",
                f"- Source title occurrences matched to canonical names: `{summary['sourceTitleOccurrences']}`",
                f"- Accepted title groups: `{accepted_groups}`",
                f"- Rejected title groups: `{rejected_groups}`",
                f"- Known-description validation checks: `{validation_checks}`",
                f"- Known-description validation passes: `{validation_passes}`",
                f"- New room descriptions promoted: `{len(records)}`",
                f"- From unique-title groups: `{unique_group_records}`",
                f"- From validated duplicate-title groups: `{duplicate_group_records}`",
                f"- From identical duplicate-title groups: `{identical_title_group_records}`",
                f"- From user-rule duplicate-title groups: `{user_rule_duplicate_title_records}`",
                f"- Total descriptions after recovery: `{len(known) + len(records)}`",
                f"- Still unresolved after exhausting local WCCMP evidence: `{len(unresolved_rooms)}`",
                "",
                "## Evidence Policy",
                "",
                "`wccmp002.dat` is a WCC default-install binary source. Its room-text",
                "layout contains interleaved string fragments, so this pass only promotes",
                "records whose title identity is exact and whose duplicate-name groups are",
                "validated against existing Librarium descriptions. A final fallback uses",
                "the user-approved rule that duplicate rooms share one description and",
                "selects the most common normalized WCCMP002 extraction for that title.",
                "Existing native or reference descriptions are not overwritten.",
                "",
                "## Remaining Gap",
                "",
                "The remaining unresolved rooms are placeholder-named room records such as",
                "`Room 15/850`. The local WCCMP001/WCCMP002/WBBMP002 binaries and local",
                "OmniVerse room captures do not contain those placeholder names as",
                "recoverable room-description entries. They are listed in the JSON report",
                "as `placeholder_named_room_without_local_wcc_description_evidence`.",
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
        f"acceptedGroups={summary['acceptedGroups']} "
        f"rejectedGroups={summary['rejectedGroups']} "
        f"validationPasses={summary['validationPasses']}/{summary['validationChecks']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
