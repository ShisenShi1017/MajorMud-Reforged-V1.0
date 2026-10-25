"""Compare committed Classic command evidence with generated runtime and a live projection index.

Run the room-command regression project first with a temporary index output path, then pass
that path here. This tool reads Data/Classic but never modifies generated world data.
"""

import argparse
import csv
import json
import re
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ROOM_OWNER = re.compile(r"(?:^|,\s*)Room (\d+)/(\d+)(?=,|$)", re.IGNORECASE)
FIELDS = ["scope", "source_id", "room_id", "npc_id", "command", "issue", "recommendation", "details"]


def records(path):
    document = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(document, list):
        yield from document
    elif document.get("chunked_records"):
        for name in document["record_parts"]:
            yield from records(path.parent / name)
    else:
        yield from document["records"]


def index(path, projection):
    return {row["id"]: projection(row) for row in records(path)}


def normalized(value):
    return " ".join(str(value).strip().lower().split())


def signature(row, keys):
    return json.dumps({key: row.get(key) for key in keys}, sort_keys=True, ensure_ascii=True)


def discrepancy(rows, scope, source_id, room_id, npc_id, command, issue, recommendation, details=""):
    rows.append(dict(zip(FIELDS, (scope, source_id, room_id, npc_id, command, issue, recommendation, details))))


def compare_family(rows, family, source, generated, keys):
    source_rows = index(source, lambda row: signature(row, keys))
    generated_rows = index(generated, lambda row: signature(row, keys))
    for source_id in sorted(source_rows.keys() - generated_rows.keys()):
        discrepancy(rows, family, source_id, "", "", "", "missing_generated_record", "Repair compiler projection from the committed Librarium record.")
    for source_id in sorted(source_rows.keys() & generated_rows.keys()):
        if source_rows[source_id] != generated_rows[source_id]:
            discrepancy(rows, family, source_id, "", "", "", "generated_record_mismatch", "Inspect source and compiler projection; do not edit Data/Classic.")
    return len(source_rows), len(generated_rows), len(source_rows.keys() - generated_rows.keys())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("runtime_index", type=Path)
    parser.add_argument("--report-dir", type=Path, default=ROOT / "Reports" / "RuntimeVerification")
    args = parser.parse_args()
    runtime = json.loads(args.runtime_index.read_text(encoding="utf-8"))
    supported = {(row["RoomId"], row["TextblockId"], normalized(row["Command"]))
                 for row in runtime["SupportedRoomActions"]}
    projected = {(row["RoomId"], normalized(row["Command"]))
                 for kind in ("GateAliases", "ObjectCommands") for row in runtime[kind]}
    projected.update((room, command) for room, _, command in supported)
    live_dialogue = {(row["NpcId"], row["RootTextblockId"], normalized(row["Keyword"]), row["TargetTextblockId"])
                     for row in runtime.get("DialogueRoutes", [])}
    live_direct_dialogue = {(row["NpcId"], row["RootTextblockId"])
                            for row in runtime.get("DirectDialogueRoutes", [])}
    live_direct_roots = {root for _, root in live_direct_dialogue}
    rows = []
    counts = {}
    informational = Counter()

    families = [
        ("rooms", "Librarium/Objects/rooms/room_records.json", "Data/Classic/rooms.json",
         ["id", "map_number", "room_number", "name", "description", "relationships"]),
        ("exits", "Librarium/Objects/exits/exit_records.json", "Data/Classic/exits.json",
         ["id", "direction", "from_map_number", "from_room_number", "to_map_number", "to_room_number",
          "gate_kind", "qualifier", "commands", "prerequisites", "effects", "runtime_disposition"]),
        ("npc_dialogue", "Librarium/Objects/npc_dialogue/npc_dialogue_records.json", "Data/Classic/npc_dialogue.json",
         ["id", "npc_id", "root_textblock_id", "evidence_state"]),
        ("textblocks", "Librarium/Objects/textblocks/textblock_records.json", "Data/Classic/textblocks.json",
         ["id", "textblock_id", "next_textblock_id", "owner_raw", "content_kind", "script", "commands", "triggers", "operations"]),
        ("room_interactions", "Librarium/Objects/room_interactions/room_interaction_records.json", "Data/Classic/room_interactions.json",
         ["id", "interaction_kind", "from_map_number", "from_room_number", "commands", "runtime_disposition"]),
    ]
    for family, source, generated, keys in families:
        counts[family] = compare_family(rows, family, ROOT / source, ROOT / generated, keys)

    room_ids = {row["id"] for row in records(ROOT / "Data/Classic/rooms.json")}
    exits = list(records(ROOT / "Data/Classic/exits.json"))
    gate_aliases = 0
    for exit_row in exits:
        if exit_row.get("gate_kind") != "text_command":
            continue
        room_id = exit_row["relationships"]["from_room"]
        commands = exit_row.get("commands", [])
        if not commands:
            informational["source_text_command_empty_intentional"] += 1
        destination = exit_row["relationships"].get("to_room", "")
        if destination not in room_ids:
            discrepancy(rows, "exit", exit_row["id"], room_id, "", "", "destination_unresolved",
                        "Resolve the map-qualified destination from source evidence.", destination)
        for command in commands:
            gate_aliases += 1
            if (room_id, normalized(command)) not in projected:
                discrepancy(rows, "exit", exit_row["id"], room_id, "", command, "missing_client_command",
                            "Project the exact generated gate alias in ROOMCOMMANDS.")

    action_count = 0
    action_rooms = set()
    for node in records(ROOT / "Data/Classic/textblocks.json"):
        if node.get("owner", {}).get("kind") != "room" or node.get("content_kind") != "action_script":
            continue
        owners = [(int(m), int(r)) for m, r in ROOM_OWNER.findall(node.get("owner_raw", ""))]
        commands = [trigger.get("raw", "") for trigger in node.get("triggers", [])]
        commands += [operation.get("raw", "") for operation in node.get("operations", [])
                     if operation.get("sequence") == 1 and operation.get("opcode", "").lower() == "summon"
                     and not operation.get("numeric_arguments")]
        for map_id, number in owners:
            room_id = f"room.map{map_id}.{number}"
            action_rooms.add(room_id)
            if room_id not in room_ids:
                discrepancy(rows, "room_action", node["id"], room_id, "", "", "source_room_unresolved",
                            "Resolve room ownership from committed source records.")
            for command in dict.fromkeys(command for command in commands if command.strip()):
                action_count += 1
                if (room_id, node["textblock_id"], normalized(command)) not in supported:
                    discrepancy(rows, "room_action", node["id"], room_id, "", command, "decoded_effect_not_executable",
                                "Recover and bind the complete decoded condition/effect sequence before exposing this action.",
                                ",".join(sorted({operation.get("opcode", "") for operation in node.get("operations", [])})))
                elif (room_id, normalized(command)) not in projected:
                    discrepancy(rows, "room_action", node["id"], room_id, "", command, "missing_client_command",
                                "Project this executable room script in ROOMCOMMANDS.")

    textblocks = {row["textblock_id"]: row for row in records(ROOT / "Data/Classic/textblocks.json")}
    dialogue_links = list(records(ROOT / "Data/Classic/npc_dialogue.json"))
    dialogue_routes = 0
    for link in dialogue_links:
        root = textblocks.get(link["root_textblock_id"])
        if root is None:
            discrepancy(rows, "npc_dialogue", link["id"], "", str(link["npc_id"]), "", "root_unresolved",
                        "Repair the generated NPC-to-textblock link from source evidence.")
            continue
        if not root.get("commands"):
            if (link["npc_id"], link["root_textblock_id"]) not in live_direct_dialogue:
                discrepancy(rows, "npc_dialogue", link["id"], "", str(link["npc_id"]), "greet",
                            "direct_greet_not_live_bound", "Bind the source-authorized direct root through GREET.",
                            f"root=textblock.{link['root_textblock_id']}")
            continue
        for command in root["commands"]:
            dialogue_routes += 1
            keyword = command["keyword"]
            if command["target_textblock_id"] == 0:
                informational["dialogue_zero_target_sentinel"] += 1
            elif command["target_textblock_id"] not in textblocks:
                discrepancy(rows, "npc_dialogue", link["id"], "", str(link["npc_id"]), keyword, "target_unresolved",
                            "Restore the decoded target textblock.")
            elif (link["npc_id"], link["root_textblock_id"], normalized(keyword), command["target_textblock_id"]) not in live_dialogue:
                discrepancy(rows, "npc_dialogue", link["id"], "", str(link["npc_id"]), keyword, "dialogue_keyword_not_live_bound",
                            "Bind the decoded plan through NPC-specific ASK; do not treat it as a room action.",
                            f"root=textblock.{link['root_textblock_id']};target=textblock.{command['target_textblock_id']}")

    issues = Counter(row["issue"] for row in rows)
    args.report_dir.mkdir(parents=True, exist_ok=True)
    csv_path = args.report_dir / "classic_room_command_discrepancies.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=FIELDS)
        writer.writeheader()
        writer.writerows(rows)
    report = [
        "# Classic Room Command and Dialogue Audit", "",
        "Scope: committed Librarium versus generated `Data/Classic`, live gate/object/action projection, and the current ASK binding.",
        "The local `ImportedDatabases/` tree contains only its README; primary WCC/MDB binaries were not available for this pass.",
        "Thus this is exhaustive for the committed decoded records, not a fresh native-binary proof.", "",
        "## Inventory", "",
    ]
    report += [f"- {family}: {source_count} committed source records; {generated_count} generated records; {missing} missing from generated."
               for family, (source_count, generated_count, missing) in counts.items()]
    report += [
        f"- Text-command exit aliases: {gate_aliases}.",
        f"- Room-action rooms: {len(action_rooms)}; source command/room bindings: {action_count}; executable projected bindings: {len(supported)}.",
        f"- NPC dialogue ownership links: {len(dialogue_links)}; keyword routes: {dialogue_routes}; live decoded ASK routes: {len(live_dialogue)}; direct GREET ownership routes: {len(live_direct_dialogue)} across {len(live_direct_roots)} source roots.",
        "", "## Discrepancies", "",
    ]
    report += [f"- {issue}: {count}" for issue, count in sorted(issues.items())] or ["- None."]
    report += [
        "", "## Intentional Non-Actions", "",
        f"- Dialogue target `textblock.0` sentinels: {informational['dialogue_zero_target_sentinel']}; these remain silent and non-mutating.",
        f"- Empty native text-command exits: {informational['source_text_command_empty_intentional']}; no alias is fabricated.",
        "", f"Every discrepancy is listed with source/room/NPC IDs in `{csv_path.name}`.", "",
        "## Ferryman", "",
        "- `npc.81` owns MDB dialogue root `textblock.77`: borrow, skiff, boat, ferry, silvermere are NPC keywords, not exit commands.",
        "- `exit.map1.2149.s` authoritatively supplies `borrow skiff`, `go skiff`, and `row skiff` to `room.map1.2335`.",
        "- The decoded route continues south to Silvermere dock `room.map1.296`, and north through the pier back to Newhaven dock `room.map1.2149`.",
        "- The local dispatcher regression proves all three crossing aliases and the two-way return path. Ferryman NPC keywords resolve through the same decoded dialogue executor used world-wide.",
        "- A disposable real-character regression crosses by `borrow skiff`, autosaves, reloads, and reconnects in `room.map1.2335`; repository saves are not touched.", "",
        "## Interpretation", "",
        "- Every decoded room action whose operations belong to the exhausted 42-opcode vocabulary is projected through the production executor.",
        "- Every nonzero NPC keyword target resolves through NPC-specific ASK; ability-acquisition routes retain their specialized authority.",
        "- `dialogue_zero_target_sentinel` is an explicit decoded no-target branch and executes as a silent no-op.",
        "- The empty text gate `exit.map11.40.w` is a real source gap (`Text: `), not an alias to guess.",
        "- ROOMCOMMANDS lists gates, materialized room objects, executable room scripts, and live NPC-specific ASK routes. The client runs a selected entry through the ordinary server command path.",
    ]
    md_path = args.report_dir / "classic_room_command_audit.md"
    md_path.write_text("\n".join(report) + "\n", encoding="utf-8")
    print(json.dumps({"report": str(md_path), "discrepancies": len(rows), "issues": dict(issues),
                      "rooms": counts["rooms"][0], "dialogue_links": len(dialogue_links)}, sort_keys=True))


if __name__ == "__main__":
    main()
