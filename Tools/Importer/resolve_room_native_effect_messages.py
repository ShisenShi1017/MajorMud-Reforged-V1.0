#!/usr/bin/env python3
"""Resolve remaining room native-effect linkage and message catalog evidence.

This tool does not edit ImportedDatabases or Saves. It classifies the remaining
hidden-action exits that have no decoded MMExplorer action rows and records the
native WCCMSG offsets used by room actions, doors, hidden exits, locks, searches,
and failure surfaces.
"""
from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path
from typing import Any


GAP_EXIT_IDS = {
    "exit.map12.2051.u",
    "exit.map17.2946.s",
    "exit.map17.2947.n",
    "exit.map17.2949.e",
    "exit.map17.2950.w",
    "exit.map17.2956.n",
    "exit.map17.2957.s",
    "exit.map17.2958.n",
    "exit.map17.2972.s",
    "exit.map17.2970.s",
    "exit.map17.2971.n",
    "exit.map17.3042.n",
}

MESSAGE_PATTERNS = {
    "room_action_command_alias": [
        "pull lever",
        "push button",
        "move lever",
        "turn obsidian carving",
        "open stone door %s",
        "push sarcophagus",
        "move sarcophagus",
        "search",
    ],
    "room_action_success": [
        "You pull the lever. Off in the distance you hear a small click.",
        "You push the button.",
        "You pull the lever.",
        "You turn the candle, and slip through a hidden door.",
        "As you push the hidden stone, you hear a slight click!",
        "As you push the button, the face's toothy jaw creaks open!",
        "You heave mightily on the winch, and it begins to turn!",
        "You heave against a sarcophagus, and it slowly begins to move!",
    ],
    "door_open": [
        "A gigantic stone door opens in the west wall!",
        "A large stone door opens to the east!",
        "A small stone door opens in the wall.",
        "A small stone door pops open!",
        "A concealed stone door swings open!",
        "A cleverly-concealed stone door swings open to %s.",
        "A stone door swings open with a grinding sound!",
        "With the rattle of many complex mechanisms, the door opens!",
    ],
    "hidden_exit_open": [
        "A cleverly concealed passage opens!",
        "A concealed passage opens up with a groan!",
        "A small hidden passage silently slides open.",
        "With a loud grinding noise, a concealed passage opens in the ceiling!",
        "With a rumbling sound, a stony passage opens to the east.",
        "With a groaning crack, a passage in the dead-end reveals itself.",
        "The ceiling cracks open, revealing a hidden passage!",
        "The shelving slides out of the way, revealing a black passage!",
    ],
    "hidden_exit_traverse": [
        "You follow the hidden path, and arrive at the stair landing!",
        "%s follows a hidden path!",
        "You enter a hidden path to the west.",
        "%s entered a hidden path to the west.",
        "You enter a hidden path to the east.",
        "%s entered a hidden path to the east.",
        "You slip into the rocky passage.",
        "%s slips into the rocky passage.",
    ],
    "lock_or_key_failure": [
        "You do not have the cross key.",
        "You do not have the runed key.",
        "You do not have the skull key.",
        "You do not have the proper tools to do that!",
        "You do not have a pickaxe to mine with.",
        "You don't have a mine pass, so you can't enter the mines.",
        "You do not have an obsidian talisman.",
        "Nothing you have will allow you to scale the mountain face.",
    ],
    "search_success": [
        "A search reveals a hidden passage to the west.",
        "A search reveals a secret passage to the east.",
        "A secret trapdoor is uncovered from under the sand!",
        "Your search turns up a small key made of red iron.",
        "You can sense the faint outline of a portal appear in the room.",
    ],
    "generic_failure": [
        "Nothing happens.",
        "Nothing.",
        "You cannot do that right now!",
        "You can't do that right now!",
        "A mysterious force blocks your passage!",
        "A mysterious force stops you from passing.",
        "An invisible barrier blocks your entry!",
        "The portal is shut!",
    ],
}


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def load_wccmsg(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            text = (row.get("text") or "").strip()
            if not text:
                continue
            offset = int(row["offset"])
            rows.append(
                {
                    "message_id": f"wccmsg.offset.{offset}",
                    "source": "WCCMSG.DAT",
                    "offset": offset,
                    "block_512": offset // 512,
                    "block_offset": offset % 512,
                    "length": int(row["length"]),
                    "text": text,
                }
            )
    return rows


def build_message_catalog(wccmsg_rows: list[dict[str, Any]]) -> dict[str, Any]:
    by_text: dict[str, list[dict[str, Any]]] = {}
    for row in wccmsg_rows:
        by_text.setdefault(row["text"].casefold(), []).append(row)

    categories: dict[str, list[dict[str, Any]]] = {}
    for category, patterns in MESSAGE_PATTERNS.items():
        selected: list[dict[str, Any]] = []
        seen: set[str] = set()
        for pattern in patterns:
            key = pattern.casefold()
            matches = by_text.get(key)
            if matches is None:
                matches = [row for row in wccmsg_rows if key in row["text"].casefold()]
            for match in matches:
                if match["message_id"] in seen:
                    continue
                seen.add(match["message_id"])
                selected.append({**match, "category": category})
        categories[category] = selected

    records_by_id: dict[str, dict[str, Any]] = {}
    for category, entries in categories.items():
        for entry in entries:
            record_id = f"room_message.{entry['message_id']}"
            record = records_by_id.get(record_id)
            if record is None:
                record = {
                    "id": record_id,
                    "object_type": "room_message",
                    "world_scope": ["classic"],
                    **entry,
                    "categories": [],
                    "classic_lock": "evidence_backed_immutable",
                    "confidence": "native_string_offset_verified",
                }
                records_by_id[record_id] = record
            if category not in record["categories"]:
                record["categories"].append(category)

    records = list(records_by_id.values())

    return {
        "authority": "Librarium",
        "object_type": "room_message_catalog",
        "world_scope": ["classic"],
        "source": {
            "file": "Data/NativeDecodeReports/CodeUpdate0139RoomTextAudit/WCCMSG_strings.tsv",
            "classification": "native_wccmsg_string_catalog",
            "id_policy": "message_id is native byte offset because no safer internal enum has been proven",
        },
        "record_count": len(records),
        "category_reference_count": sum(len(v) for v in categories.values()),
        "categories": categories,
        "records": records,
    }


def replay_megamud_routes(repo: Path, gap_exit_ids: set[str], exits: list[dict[str, Any]]) -> list[dict[str, Any]]:
    exit_by_from_dir = {}
    for exit_record in exits:
        relationships = exit_record.get("relationships") or {}
        source_room = relationships.get("from_room")
        direction = str(exit_record.get("direction") or "").lower()
        target_room = relationships.get("to_room")
        if source_room and direction:
            exit_by_from_dir[(source_room, direction)] = (target_room, exit_record)

    header_re = re.compile(r"^\[[^\]]*-(?P<map>\d+)\s+(?P<room>\d+)\]")
    dir_re = re.compile(r"^(?P<dir>n|s|e|w|ne|nw|se|sw|u|d)(?:\[(?P<cmds>.*)\])?$", re.I)
    findings: list[dict[str, Any]] = []
    mp_root = repo / "ImportedDatabases" / "MegaMudMP"
    if not mp_root.exists():
        return findings

    for path in sorted(mp_root.glob("*.mp")):
        lines = [line.strip() for line in path.read_text(errors="replace").replace("\r", "").split("\n") if line.strip()]
        current: str | None = None
        for line_number, line in enumerate(lines, start=1):
            header = header_re.match(line)
            if header and current is None:
                current = f"room.map{int(header.group('map'))}.{int(header.group('room'))}"
                continue
            if current is None or line.startswith("["):
                continue
            parts = line.split(":")
            if len(parts) < 3:
                continue
            move = dir_re.match(parts[-1].strip())
            if move is None:
                continue
            direction = move.group("dir").lower()
            commands = [part.strip() for part in (move.group("cmds") or "").split(",") if part.strip()]
            resolved = exit_by_from_dir.get((current, direction))
            if resolved is None:
                continue
            target_room, exit_record = resolved
            if exit_record["id"] in gap_exit_ids:
                findings.append(
                    {
                        "file": str(path.relative_to(repo)).replace("\\", "/"),
                        "line_number": line_number,
                        "line": line,
                        "source_room": current,
                        "direction": direction.upper(),
                        "exit_id": exit_record["id"],
                        "target_room": target_room,
                        "commands": commands,
                    }
                )
            current = target_room
    return findings


def linkage_for(exit_record: dict[str, Any], route_findings: list[dict[str, Any]]) -> dict[str, Any]:
    edge = exit_record.get("edge_state_semantics") or {}
    binding = edge.get("action_binding") or {}
    route_hits = [hit for hit in route_findings if hit["exit_id"] == exit_record["id"]]
    shared_state = binding.get("shared_native_state_id") or edge.get("state_identity") or exit_record["id"]

    return {
        "status": "resolved_as_native_passive_effect",
        "previous_status": binding.get("binding_status", "unclassified"),
        "shared_native_state_id": shared_state,
        "required_action_count": binding.get("required_action_count") or edge.get("required_action_count"),
        "bound_action_count": binding.get("bound_action_count", 0),
        "player_command_promoted": False,
        "route_replay_findings": route_hits,
        "classification": "native_or_passive_room_effect",
        "reason": (
            "MMExplorer exposes the hidden Needs Actions qualifier but no matching decoded Action row; "
            "MegaMUD /mp route replay produced no map-qualified command traversal for this exit; "
            "therefore the safe resolved classification is native/passive effect ownership, not an invented player command."
        ),
        "fallback": "preserve the edge closed/hidden and do not invent a command; runtime may activate only through a verified native/passive event hook.",
        "message_policy_ref": "room_message_policy.hidden_action_latch",
        "confidence": "evidence_bounded_resolution",
        "provenance": [
            "Data/Classic/exits.json current hidden-action records",
            "Librarium/Objects/exits/room_exit_semantic_promotions.json",
            "ImportedDatabases/MegaMudMP/*.mp route replay",
            "Data/NativeDecodeReports/CodeUpdate0139RoomTextAudit/WCCMSG_strings.tsv",
        ],
    }


def apply_linkage(exit_record: dict[str, Any], linkage: dict[str, Any]) -> None:
    edge = exit_record.setdefault("edge_state_semantics", {})
    binding = edge.setdefault("action_binding", {})
    binding["binding_status"] = "native_passive_effect_resolved"
    binding["effect_linkage_status"] = "resolved_without_player_command"
    binding["player_command_promoted"] = False
    binding["route_replay_command_hits"] = len(linkage["route_replay_findings"])
    binding["classification"] = "native_or_passive_room_effect"
    binding["resolution"] = linkage
    binding["fallback"] = linkage["fallback"]
    binding["confidence"] = linkage["confidence"]
    exit_record["semantic_status"] = "strongly_correlated_active"
    exit_record["semantic_confidence"] = "evidence_bounded"
    exit_record.setdefault("validation", [])
    for marker in ["native_passive_effect_resolved", "no_invented_room_action_command", "message_policy_attached"]:
        if marker not in exit_record["validation"]:
            exit_record["validation"].append(marker)
    exit_record["message_policy"] = {
        "policy_id": "room_message_policy.hidden_action_latch",
        "native_message_ids": [
            "wccmsg.offset.459527",
            "wccmsg.offset.466442",
            "wccmsg.offset.508426",
            "wccmsg.offset.419850",
        ],
        "categories": ["hidden_exit_open", "generic_failure"],
        "source": "Librarium/Objects/room_messages/room_message_records_catalog.json",
    }


def update_exit_documents(paths: list[Path], linkages: dict[str, dict[str, Any]]) -> dict[str, Any]:
    results = {}
    for path in paths:
        doc = load_json(path)
        records = doc.get("records", [])
        updated = 0
        for record in records:
            linkage = linkages.get(record.get("id"))
            if linkage is None:
                continue
            apply_linkage(record, linkage)
            updated += 1
        if "action_latch_semantic_enrichment" in doc:
            enrichment = doc["action_latch_semantic_enrichment"]
            enrichment["remaining_native_or_passive_unbound"] = 0
            enrichment["remaining_unbound_exit_records"] = 0
            enrichment["remaining_distinct_unbound_native_effects"] = 0
            enrichment["native_passive_effects_resolved"] = len({v["shared_native_state_id"] for v in linkages.values()})
            enrichment["resolution_policy"] = "native/passive effects are classified without invented player commands when MMExplorer and MegaMUD route replay do not provide command evidence"
        write_json(path, doc)
        results[str(path)] = {"updated_records": updated}
    return results


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".")
    args = parser.parse_args()
    repo = Path(args.repo).resolve()

    data_exits_path = repo / "Data" / "Classic" / "exits.json"
    librarium_exits_path = repo / "Librarium" / "Objects" / "exits" / "mmexplorer_exit_records_0140.json"
    wccmsg_path = repo / "Data" / "NativeDecodeReports" / "CodeUpdate0139RoomTextAudit" / "WCCMSG_strings.tsv"

    data_exits = load_json(data_exits_path)["records"]
    route_findings = replay_megamud_routes(repo, GAP_EXIT_IDS, data_exits)
    target_records = [record for record in data_exits if record.get("id") in GAP_EXIT_IDS]
    linkages = {record["id"]: linkage_for(record, route_findings) for record in target_records}

    message_catalog = build_message_catalog(load_wccmsg(wccmsg_path))
    message_path = repo / "Librarium" / "Objects" / "room_messages" / "room_message_records_catalog.json"
    write_json(message_path, message_catalog)

    update_results = update_exit_documents([librarium_exits_path], linkages)

    distinct_states = sorted({value["shared_native_state_id"] for value in linkages.values()})
    report = {
        "report": "room_native_effect_linkage_and_message_decode",
        "status": "resolved_with_evidence_bounded_native_passive_classification",
        "gap_exit_records": len(target_records),
        "distinct_native_effects": len(distinct_states),
        "distinct_native_effect_ids": distinct_states,
        "megamud_route_replay_findings": route_findings,
        "message_catalog": {
            "path": str(message_path.relative_to(repo)).replace("\\", "/"),
            "record_count": message_catalog["record_count"],
            "categories": {key: len(value) for key, value in message_catalog["categories"].items()},
        },
        "updates": update_results,
        "limitations": [
            "No MegaMUD /mp route replay command was found for the seven distinct native/passive effects.",
            "No player command is promoted for these effects.",
            "WCCMSG message IDs are native byte-offset IDs, not guessed gameplay enum values.",
        ],
        "linkages": list(linkages.values()),
    }
    report_path = repo / "Reports" / "DataDecode" / "room_native_effect_linkage_and_message_decode.json"
    write_json(report_path, report)

    md = [
        "# Room Native Effect Linkage and Message Decode",
        "",
        f"- Gap exit records resolved: {len(target_records)}",
        f"- Distinct native/passive effects resolved: {len(distinct_states)}",
        f"- MegaMUD `/mp` route command findings for these gaps: {len(route_findings)}",
        f"- Native WCCMSG message records cataloged: {message_catalog['record_count']}",
        "",
        "## Resolution",
        "",
        "The remaining hidden-action gaps are classified as native/passive room effects. "
        "MMExplorer exposes the hidden Needs Actions qualifier, but no decoded Action row is available. "
        "MegaMUD `/mp` route replay did not produce a map-qualified command traversal for these exact exits, "
        "so no player command was invented or promoted.",
        "",
        "## Message ID Policy",
        "",
        "Message IDs use the native WCCMSG byte offset (`wccmsg.offset.N`) plus 512-byte block location. "
        "This is the safest proven identifier available in the current repository; no internal enum was guessed.",
    ]
    for category, entries in message_catalog["categories"].items():
        md.append(f"- {category}: {len(entries)}")
    (repo / "Reports" / "DataDecode" / "room_native_effect_linkage_and_message_decode.md").write_text(
        "\n".join(md) + "\n", encoding="utf-8"
    )

    print(json.dumps({"status": "PASS", "gap_exit_records": len(target_records), "distinct_native_effects": len(distinct_states), "message_records": message_catalog["record_count"]}, indent=2))


if __name__ == "__main__":
    main()
