#!/usr/bin/env python3
"""Decode the MMExplorer Rooms table directly from data-v1.11n.mdb.

This decoder is intentionally table-specific.  The Jet table-definition page at
owner page 1664 identifies the 21 columns used by MMExplorer's Rooms table:
Map Number, Room Number, Name, Light, Shop, NPC, CMD, Spell, Lair, Delay,
Placed, N, S, E, W, NE, NW, SE, SW, U, and D.

The decoder emits map-qualified room and exit records.  It preserves gate text
such as ``(Door)`` and ``(Text: climb tree)`` as evidence instead of inventing
an item, level, flag, or spell prerequisite.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

PAGE_SIZE = 4096
ROOM_OWNER_TABLE_PAGE = 1664
ROOM_COLUMN_COUNT = 21
VARIABLE_COLUMN_COUNT = 13
VARIABLE_COLUMNS = (
    "name", "npc", "cmd", "n", "s", "e", "w", "ne", "nw", "se", "sw", "u", "d"
)
DIRECTION_COLUMNS = VARIABLE_COLUMNS[3:]
EXIT_RE = re.compile(r"^\s*(?P<map>\d+)\s*/\s*(?P<room>\d+)(?P<suffix>[A-Za-z]*)(?:\s*\((?P<qualifier>.*)\))?\s*$")
TEXT_GATE_RE = re.compile(r"^text\s*:\s*(?P<commands>.*)$", re.IGNORECASE)
ACTION_RE = re.compile(
    r"^Action(?:#(?P<number>\d+))?\s*\[on the (?P<direction>N|S|E|W|NE|NW|SE|SW|U|D) exit of "
    r"(?P<room>this room|room\s+\d+\s*/\s*\d+)\]\s*:\s*(?P<commands>.*)$",
    re.IGNORECASE,
)
DOOR_RE = re.compile(r"^door(?:\s*\[(?P<difficulty>any|\d+)\s+picklocks/strength\])?$", re.IGNORECASE)
ITEM_RE = re.compile(r"^item\s*:\s*(?P<item_id>\d+)$", re.IGNORECASE)
TOLL_RE = re.compile(r"^toll\s*:\s*(?P<amount>\d+)$", re.IGNORECASE)
TRAP_RE = re.compile(r"^trap\s*,\s*(?P<damage>\d+)\s+damage$", re.IGNORECASE)
SPELL_TRAP_RE = re.compile(r"^spell trap\s*:\s*(?P<spell_id>\d+)$", re.IGNORECASE)
CAST_RE = re.compile(r"^cast\s*:\s*pre-(?P<pre>\d+)\s*,\s*post-(?P<post>\d+)$", re.IGNORECASE)
HIDDEN_RE = re.compile(r"^hidden/(?P<mode>passable|searchable|needs\s+\d+\s+actions?,\s*(?:any|specific)\s+order)$", re.IGNORECASE)


@dataclass(frozen=True)
class RawRow:
    page: int
    row_index_on_page: int
    offset: int
    row_pointer_raw: int
    blob: bytes


def parse_data_page(page: bytes, page_number: int) -> Iterable[RawRow]:
    if len(page) != PAGE_SIZE or page[0] != 0x01:
        return ()
    owner = int.from_bytes(page[4:8], "little")
    if owner != ROOM_OWNER_TABLE_PAGE:
        return ()
    row_count = int.from_bytes(page[12:14], "little")
    pointers: list[tuple[int, int]] = []
    for index in range(row_count):
        raw = int.from_bytes(page[14 + index * 2:16 + index * 2], "little")
        offset = raw & 0x0FFF
        if 0 < offset < PAGE_SIZE:
            pointers.append((offset, raw))
    rows: list[RawRow] = []
    for index, (offset, raw_pointer) in enumerate(pointers):
        end = pointers[index - 1][0] if index else PAGE_SIZE
        if end > offset:
            rows.append(RawRow(page_number, index, offset, raw_pointer, page[offset:end]))
    return rows


def decode_jet_text(value: bytes) -> str:
    """Decode Access Unicode-compression values used by this table."""
    if not value:
        return ""
    if value.startswith(b"\xff\xfe"):
        # Jet Unicode compression: marker followed by single-byte characters.
        return value[2:].rstrip(b"\x00").decode("latin-1", errors="replace").strip()
    if len(value) % 2:
        # Preserve malformed evidence rather than silently dropping a byte.
        value += b"\x00"
    return value.decode("utf-16le", errors="replace").rstrip("\x00").strip()


def decode_variable_columns(blob: bytes) -> dict[str, str]:
    null_mask_bytes = math.ceil(ROOM_COLUMN_COUNT / 8)
    count_position = len(blob) - null_mask_bytes - 2
    if count_position < 0:
        raise ValueError("row is too short for variable-column metadata")
    variable_count = int.from_bytes(blob[count_position:count_position + 2], "little")
    if variable_count != VARIABLE_COLUMN_COUNT:
        raise ValueError(f"expected {VARIABLE_COLUMN_COUNT} variable columns, found {variable_count}")
    offsets_position = count_position - 2 * (variable_count + 1)
    if offsets_position < 0:
        raise ValueError("row is too short for variable-column offsets")
    descending = [
        int.from_bytes(blob[offsets_position + i * 2:offsets_position + i * 2 + 2], "little")
        for i in range(variable_count + 1)
    ]
    offsets = list(reversed(descending))
    if offsets != sorted(offsets) or offsets[0] < 0 or offsets[-1] > offsets_position:
        raise ValueError(f"invalid variable-column offsets: {offsets}")
    values = [decode_jet_text(blob[start:end]) for start, end in zip(offsets, offsets[1:])]
    return dict(zip(VARIABLE_COLUMNS, values, strict=True))


def read_fixed_fields(blob: bytes) -> dict[str, int | str]:
    if len(blob) < 34 or blob[0] != ROOM_COLUMN_COUNT:
        raise ValueError("row does not match the MMExplorer Rooms fixed layout")
    # Map and room offsets are verified by the Rooms table definition and all
    # decoded rows. Remaining fixed bytes are retained losslessly until each
    # numeric field's exact width/signedness is independently verified.
    return {
        "map_number": int.from_bytes(blob[2:4], "little"),
        "room_number": int.from_bytes(blob[6:8], "little"),
        "fixed_prefix_hex": blob[:34].hex(),
    }


def split_commands(raw: str) -> list[str]:
    return [part.strip() for part in raw.split(",") if part.strip()]


def parse_qualifier(qualifier: str) -> dict:
    if not qualifier:
        return {"gate_kind": "open", "prerequisites": {}}
    door = DOOR_RE.match(qualifier)
    if door:
        difficulty = door.group("difficulty")
        return {
            "gate_kind": "door",
            "prerequisites": {
                "picklocks_or_strength": None if not difficulty or difficulty.lower() == "any" else int(difficulty),
                "any_picklocks_or_strength": bool(difficulty and difficulty.lower() == "any"),
            },
        }
    item = ITEM_RE.match(qualifier)
    if item:
        return {"gate_kind": "required_item", "prerequisites": {"required_item_id": int(item.group("item_id"))}}
    toll = TOLL_RE.match(qualifier)
    if toll:
        return {"gate_kind": "toll", "prerequisites": {"toll_amount": int(toll.group("amount"))}}
    trap = TRAP_RE.match(qualifier)
    if trap:
        return {"gate_kind": "damage_trap", "effects": {"damage": int(trap.group("damage"))}, "prerequisites": {}}
    spell_trap = SPELL_TRAP_RE.match(qualifier)
    if spell_trap:
        return {"gate_kind": "spell_trap", "effects": {"spell_id": int(spell_trap.group("spell_id"))}, "prerequisites": {}}
    cast = CAST_RE.match(qualifier)
    if cast:
        return {
            "gate_kind": "cast_transition",
            "effects": {"pre_spell_id": int(cast.group("pre")), "post_spell_id": int(cast.group("post"))},
            "prerequisites": {},
        }
    hidden = HIDDEN_RE.match(qualifier)
    if hidden:
        mode = hidden.group("mode").lower()
        result = {"gate_kind": "hidden", "hidden_mode": mode, "prerequisites": {}}
        needs = re.search(r"needs\s+(\d+)\s+actions?,\s*(any|specific)\s+order", mode)
        if needs:
            result["prerequisites"] = {
                "required_action_count": int(needs.group(1)),
                "action_order": needs.group(2),
            }
        return result
    text_match = TEXT_GATE_RE.match(qualifier)
    if text_match:
        return {"gate_kind": "text_command", "commands": split_commands(text_match.group("commands")), "prerequisites": {}}
    return {"gate_kind": "qualified_unknown", "prerequisites": {}}


def parse_exit(raw_value: str, direction: str) -> dict | None:
    value = raw_value.strip()
    if not value or value == "0":
        return None
    action = ACTION_RE.match(value)
    if action:
        source_room = action.group("room")
        source_map_number = source_room_number = None
        if source_room.lower() != "this room":
            numbers = re.search(r"(\d+)\s*/\s*(\d+)", source_room)
            if numbers:
                source_map_number, source_room_number = int(numbers.group(1)), int(numbers.group(2))
        return {
            "record_kind": "interaction",
            "interaction_kind": "exit_action",
            "action_number": int(action.group("number")) if action.group("number") else None,
            "direction": action.group("direction").upper(),
            "explicit_source_map_number": source_map_number,
            "explicit_source_room_number": source_room_number,
            "commands": split_commands(action.group("commands")),
            "raw_value": value,
            "status": "decoded_action",
            "runtime_disposition": "interaction_visible",
        }
    match = EXIT_RE.match(value)
    if not match:
        return {
            "record_kind": "interaction",
            "interaction_kind": "unparsed_direction_definition",
            "direction": direction.upper(),
            "raw_value": value,
            "status": "unparsed_reference",
            "runtime_disposition": "visible_not_traversable",
        }
    target_map = int(match.group("map"))
    target_room = int(match.group("room"))
    suffix = match.group("suffix") or ""
    qualifier = (match.group("qualifier") or "").strip()
    parsed_qualifier = parse_qualifier(qualifier)
    result = {
        "record_kind": "exit",
        "direction": direction.upper(),
        "to_map_number": target_map,
        "to_room_number": target_room,
        "to_room_id": f"room.map{target_map}.{target_room}",
        "raw_value": value,
        "reference_suffix": suffix,
        "qualifier": qualifier,
        "status": "decoded",
    }
    result.update(parsed_qualifier)
    result.setdefault("commands", [])
    result.setdefault("effects", {})
    result["runtime_disposition"] = "traversable" if result["gate_kind"] == "open" else "gate_evidence_preserved"
    return result


def decode_rooms(mdb_path: Path) -> tuple[list[dict], list[dict], list[dict], list[dict]]:
    data = mdb_path.read_bytes()
    rooms: list[dict] = []
    exits: list[dict] = []
    interactions: list[dict] = []
    errors: list[dict] = []
    for page_number in range(len(data) // PAGE_SIZE):
        page = data[page_number * PAGE_SIZE:(page_number + 1) * PAGE_SIZE]
        for row in parse_data_page(page, page_number):
            try:
                fixed = read_fixed_fields(row.blob)
                variable = decode_variable_columns(row.blob)
                map_number = int(fixed["map_number"])
                room_number = int(fixed["room_number"])
                if map_number <= 0 or room_number <= 0:
                    raise ValueError("map or room number is not positive")
                room_id = f"room.map{map_number}.{room_number}"
                room_exits: list[str] = []
                for direction in DIRECTION_COLUMNS:
                    decoded = parse_exit(variable[direction], direction)
                    if decoded is None:
                        continue
                    record_kind = decoded.pop("record_kind", "exit")
                    record_id = (
                        f"exit.map{map_number}.{room_number}.{direction}"
                        if record_kind == "exit"
                        else f"interaction.map{map_number}.{room_number}.{direction}.{len(interactions) + 1}"
                    )
                    decoded.update({
                        "id": record_id,
                        "object_type": "exit" if record_kind == "exit" else "room_interaction",
                        "from_map_number": map_number,
                        "from_room_number": room_number,
                        "from_room_id": room_id,
                        "source": {
                            "kind": "MMExplorer MDB direct Rooms-table decode",
                            "file": str(mdb_path).replace("\\", "/"),
                            "owner_table_page": ROOM_OWNER_TABLE_PAGE,
                            "page": row.page,
                            "row_index_on_page": row.row_index_on_page,
                            "offset": row.offset,
                        },
                    })
                    if record_kind == "exit":
                        exits.append(decoded)
                        room_exits.append(record_id)
                    else:
                        interactions.append(decoded)
                rooms.append({
                    "id": room_id,
                    "legacy_id": str(room_number),
                    "object_type": "room",
                    "map_id": map_number,
                    "mapId": map_number,
                    "room_number": room_number,
                    "roomNumber": room_number,
                    "name": variable["name"] or f"Room {map_number}/{room_number}",
                    "npc_definition": variable["npc"],
                    "command_definition": variable["cmd"],
                    "exit_ids": room_exits,
                    "fixed_fields_raw": fixed["fixed_prefix_hex"],
                    "source": {
                        "kind": "MMExplorer MDB direct Rooms-table decode",
                        "file": str(mdb_path).replace("\\", "/"),
                        "owner_table_page": ROOM_OWNER_TABLE_PAGE,
                        "page": row.page,
                        "row_index_on_page": row.row_index_on_page,
                        "offset": row.offset,
                        "row_pointer_flags_hex": hex(row.row_pointer_raw & 0xF000),
                    },
                    "confidence": "high_map_room_name_npc_cmd_and_direction_columns",
                })
            except Exception as exc:
                errors.append({
                    "page": row.page,
                    "row_index_on_page": row.row_index_on_page,
                    "offset": row.offset,
                    "length": len(row.blob),
                    "error": str(exc),
                    "row_hex": row.blob.hex(),
                })
    rooms.sort(key=lambda item: (item["map_id"], item["room_number"]))
    exits.sort(key=lambda item: (item["from_map_number"], item["from_room_number"], item["direction"]))
    interactions.sort(key=lambda item: (item["from_map_number"], item["from_room_number"], item["direction"], item["id"]))
    return rooms, exits, interactions, errors


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mdb", nargs="?", default="ImportedDatabases/data-v1.11n.mdb")
    parser.add_argument("--outdir", default="Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138")
    parser.add_argument("--fail-on-errors", action="store_true")
    args = parser.parse_args()
    mdb_path = Path(args.mdb)
    outdir = Path(args.outdir)
    rooms, exits, interactions, errors = decode_rooms(mdb_path)
    source_hash = hashlib.sha256(mdb_path.read_bytes()).hexdigest()
    manifest = {
        "decoder": "Tools/MDBExtractor/decode_mmexplorer_rooms.py",
        "source_file": str(mdb_path).replace("\\", "/"),
        "source_sha256": source_hash,
        "owner_table_page": ROOM_OWNER_TABLE_PAGE,
        "schema_columns": [
            "Map Number", "Room Number", "Name", "Light", "Shop", "NPC", "CMD", "Spell",
            "Lair", "Delay", "Placed", "N", "S", "E", "W", "NE", "NW", "SE", "SW", "U", "D"
        ],
        "room_count": len(rooms),
        "exit_count": len(exits),
        "interaction_count": len(interactions),
        "door_exit_count": sum(exit_["gate_kind"] == "door" for exit_ in exits if exit_.get("status") == "decoded"),
        "text_command_exit_count": sum(exit_["gate_kind"] == "text_command" for exit_ in exits if exit_.get("status") == "decoded"),
        "required_item_exit_count": sum(exit_["gate_kind"] == "required_item" for exit_ in exits),
        "toll_exit_count": sum(exit_["gate_kind"] == "toll" for exit_ in exits),
        "damage_trap_exit_count": sum(exit_["gate_kind"] == "damage_trap" for exit_ in exits),
        "spell_trap_exit_count": sum(exit_["gate_kind"] == "spell_trap" for exit_ in exits),
        "cast_transition_exit_count": sum(exit_["gate_kind"] == "cast_transition" for exit_ in exits),
        "hidden_exit_count": sum(exit_["gate_kind"] == "hidden" for exit_ in exits),
        "unknown_qualified_exit_count": sum(exit_["gate_kind"] == "qualified_unknown" for exit_ in exits if exit_.get("status") == "decoded"),
        "unparsed_interaction_count": sum(item.get("status") == "unparsed_reference" for item in interactions),
        "decode_error_count": len(errors),
        "prerequisite_policy": "Preserve explicit door/text/other qualifiers; do not invent missing item, level, flag, spell, class, race, or alignment values.",
    }
    write_json(outdir / "rooms_decoded.json", rooms)
    write_json(outdir / "exits_decoded.json", exits)
    write_json(outdir / "room_interactions_decoded.json", interactions)
    write_json(outdir / "decode_errors.json", errors)
    write_json(outdir / "decode_manifest.json", manifest)
    print(json.dumps(manifest, indent=2))
    if args.fail_on_errors and errors:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
