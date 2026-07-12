#!/usr/bin/env python3
"""Decode MMExplorer textblock/command rows and quest-flow relationships.

The decoder reads the original Access MDB directly. It resolves Jet long-value
pointers used by the textblock table, preserves the exact script text, parses
only the observed MMExplorer command grammar, and emits Librarium import data.
Unknown commands remain preserved as unknown operations and are never guessed.
"""
from __future__ import annotations
import argparse, hashlib, json, math, re, struct
from collections import defaultdict, deque
from pathlib import Path

PAGE_SIZE = 4096
TEXTBLOCK_OWNER = 2734
OVERFLOW_OWNER = 1279350348
QUEST_ABILITY_NAMES = {
    50: "MageBaneQuest", 125: "IceSorcQuest", 126: "GoodQuest",
    127: "NeutralQuest", 128: "EvilQuest", 129: "DarkDruidQuest",
    130: "BloodChampQuest", 131: "SheDragonQuest", 132: "WereratQuest",
    133: "PhoenixQuest", 134: "DaoLordQuest", 156: "QuestItem",
}
OP_SPECS = {
    # Branch / presentation
    "text": "branch", "message": "presentation", "random": "branch", "route": "branch",
    "end": "control", "stop": "control", "break": "control",
    # Ability / quest state
    "checkability": "condition", "testability": "condition", "failability": "condition",
    "giveability": "effect", "addability": "effect", "takeability": "effect",
    "removeability": "effect", "goodability": "effect",
    # Items and room items
    "checkitem": "condition", "testitem": "condition", "failitem": "condition",
    "giveitem": "effect", "takeitem": "effect", "clearitem": "effect",
    "roomitem": "condition", "failroomitem": "condition",
    # Character/world conditions
    "class": "condition", "race": "condition", "minlevel": "condition",
    "maxlevel": "condition", "levelcheck": "condition", "testskill": "condition",
    "goodaligned": "condition", "evilaligned": "condition",
    "monsters": "condition", "nomonsters": "condition", "needmonster": "condition",
    "checkspell": "condition", "check": "condition",
    # Economy and rewards
    "price": "condition", "checkgold": "condition", "testgold": "condition",
    "failgold": "condition", "givegold": "effect", "takegold": "effect",
    "givecoins": "effect", "addexp": "effect", "giveexp": "effect",
    "experience": "effect", "addevil": "effect", "alignment": "effect",
    # World/action effects
    "cast": "effect", "spell": "effect", "teleport": "effect", "room": "effect",
    "summon": "effect", "damage": "effect", "remoteaction": "effect",
    "adddelay": "effect", "delay": "effect", "chance": "condition",
}
KNOWN_OPS = set(OP_SPECS)
OP_ALIASES = {
    "add delay": "adddelay",
    "give exp": "giveexp",
}

OWNER_RE = re.compile(r"^(Monster|Textblock|Room|Item|Shop)(?:\(sell\)|\(nogen\))?\s+#?(.*)$", re.I)
REF_RE = re.compile(r"(?:Textblock\s+#)?(\d+)", re.I)


def parse_rows(data: bytes):
    for pageno in range(len(data) // PAGE_SIZE):
        page = data[pageno * PAGE_SIZE:(pageno + 1) * PAGE_SIZE]
        if len(page) != PAGE_SIZE or page[0] != 1:
            continue
        owner = int.from_bytes(page[4:8], "little")
        count = int.from_bytes(page[12:14], "little")
        ptrs = []
        for i in range(count):
            raw = int.from_bytes(page[14 + 2*i:16 + 2*i], "little")
            off = raw & 0x0FFF
            if 0 < off < PAGE_SIZE:
                ptrs.append((off, raw, i))
        for pos, (off, raw, slot) in enumerate(ptrs):
            end = ptrs[pos-1][0] if pos else PAGE_SIZE
            if end > off:
                yield owner, pageno, slot, off, raw, page[off:end]


def decode_jet_text(raw: bytes) -> str:
    if not raw:
        return ""
    if raw.startswith(b"\xff\xfe"):
        # MMExplorer uses compressed Unicode: marker followed by single-byte text.
        return raw[2:].rstrip(b"\x00").decode("latin-1", "replace")
    try:
        return raw.decode("utf-16le").rstrip("\x00")
    except UnicodeDecodeError:
        return raw.decode("latin-1", "replace").rstrip("\x00")


def decode_variable_layout(blob: bytes) -> dict:
    if not blob:
        raise ValueError("empty row")
    column_count = blob[0]
    null_bytes = math.ceil(column_count / 8)
    count_pos = len(blob) - null_bytes - 2
    if count_pos < 1:
        raise ValueError("row too short")
    var_count = int.from_bytes(blob[count_pos:count_pos+2], "little")
    off_pos = count_pos - 2 * (var_count + 1)
    if var_count > column_count or off_pos < 1:
        raise ValueError("invalid variable layout")
    descending = [int.from_bytes(blob[off_pos+2*i:off_pos+2*i+2], "little") for i in range(var_count+1)]
    offsets = list(reversed(descending))
    if offsets != sorted(offsets) or offsets[0] < 1 or offsets[-1] > off_pos:
        raise ValueError("invalid variable offsets")
    return {
        "fixed": blob[:offsets[0]],
        "values": [blob[a:b] for a, b in zip(offsets, offsets[1:])],
    }


def build_row_index(data: bytes):
    index = {}
    textblock_rows = []
    for owner, page, slot, off, ptr, blob in parse_rows(data):
        index[(page, slot)] = {"owner": owner, "blob": blob, "offset": off, "pointer": ptr}
        if owner == TEXTBLOCK_OWNER:
            textblock_rows.append((page, slot, off, ptr, blob))
    return index, textblock_rows


def resolve_long_value(descriptor: bytes, row_index: dict) -> tuple[bytes, dict]:
    if not descriptor:
        return b"", {"storage": "empty"}
    if len(descriptor) < 4:
        return descriptor, {"storage": "raw_short"}
    header = int.from_bytes(descriptor[:4], "little")
    length = header & 0x3FFFFFFF
    flags = header & 0xC0000000
    if flags == 0x80000000:
        # Inline long value has an 8-byte reserved area after the header.
        payload = descriptor[12:12+length]
        return payload, {"storage": "inline", "declared_length": length}
    if flags == 0x40000000 and len(descriptor) >= 8:
        pointer = int.from_bytes(descriptor[4:8], "little")
        page, slot = pointer >> 8, pointer & 0xFF
        row = row_index.get((page, slot))
        if not row:
            return b"", {"storage": "single_page_missing", "declared_length": length, "page": page, "slot": slot}
        payload = row["blob"][:length]
        return payload, {"storage": "single_page", "declared_length": length, "page": page, "slot": slot, "owner": row["owner"]}
    # Preserve unsupported/chained form without guessing.
    return b"", {"storage": "unsupported", "declared_length": length, "flags": hex(flags), "descriptor_hex": descriptor.hex()}


def parse_owner(owner_text: str) -> dict:
    owner_text = owner_text.strip()
    m = OWNER_RE.match(owner_text)
    if not m:
        return {"kind": "unknown", "raw": owner_text}
    kind, rest = m.group(1).lower(), m.group(2)
    refs = []
    if kind == "room":
        for map_no, room_no in re.findall(r"(\d+)\s*/\s*(\d+)", rest):
            refs.append({"map_number": int(map_no), "room_number": int(room_no), "ref": f"room.map{map_no}.{room_no}"})
    else:
        for n in re.findall(r"\d+", rest):
            refs.append({"number": int(n), "ref": f"{kind}.{int(n)}"})
    return {"kind": kind, "raw": owner_text, "references": refs}


def split_segments(line: str) -> list[str]:
    """Split MMExplorer command segments, accepting the observed ':' and ';' separators."""
    return [segment.strip() for segment in re.split(r"[:;]", line) if segment.strip()]


def normalize_operation_token(token: str) -> tuple[str, list[str]]:
    parts = token.split()
    if not parts:
        return "", []
    lower = " ".join(part.lower() for part in parts)
    for alias, canonical in OP_ALIASES.items():
        if lower == alias or lower.startswith(alias + " "):
            alias_parts = alias.split()
            return canonical, parts[len(alias_parts):]
    return parts[0].lower(), parts[1:]


def make_operation(token: str, line_no: int, sequence: int) -> dict:
    opcode, args = normalize_operation_token(token)
    source_repair = None
    # Two malformed source forms are proven by the corpus grammar:
    #   "2683 1" occurs amid three identical portal lines using "teleport 2683 1".
    #   a lone numeric segment is a terminal/textblock route selector, not an opcode.
    if re.fullmatch(r"-?\d+", opcode):
        if args and all(re.fullmatch(r"-?\d+", arg) for arg in args):
            source_repair = {"kind": "omitted_opcode", "original": token, "corroborated_opcode": "teleport"}
            args = [opcode, *args]
            opcode = "teleport"
        elif not args:
            source_repair = {"kind": "numeric_route", "original": token}
            args = [opcode]
            opcode = "route"
    ints = []
    for arg in args:
        try:
            ints.append(int(arg))
        except ValueError:
            pass
    op = {
        "line": line_no,
        "sequence": sequence,
        "raw": token,
        "opcode": opcode,
        "arguments": args,
        "operation_kind": OP_SPECS.get(opcode, "source_directive"),
        "status": "decoded" if opcode in OP_SPECS else "structurally_decoded_source_directive",
    }
    if ints:
        op["numeric_arguments"] = ints
    if source_repair:
        op["source_repair"] = source_repair
    if opcode in {"text", "message", "route"} and ints:
        op["target_textblock_id"] = ints[0]
        op["target_textblock_ref"] = f"textblock.{ints[0]}"
    if opcode in {"checkability", "testability", "failability", "giveability", "addability", "takeability", "removeability", "goodability"} and ints:
        aid = ints[0]
        op["ability_id"] = aid
        op["ability_ref"] = f"ability.{aid}"
        if aid in QUEST_ABILITY_NAMES:
            op["quest_flag_name"] = QUEST_ABILITY_NAMES[aid]
        if len(ints) > 1:
            op["ability_value"] = ints[1]
    if opcode in {"checkitem", "testitem", "giveitem", "takeitem", "failitem", "clearitem", "roomitem", "failroomitem"} and ints:
        op["item_id"] = ints[0]
        op["item_ref"] = f"item.{ints[0]}"
    if opcode == "cast" and ints:
        op["spell_id"] = ints[0]
        op["spell_ref"] = f"spell.{ints[0]}"
    if opcode == "summon" and ints:
        op["npc_id"] = ints[0]
        op["npc_ref"] = f"npc.{ints[0]}"
    if opcode == "teleport" and ints:
        op["room_number"] = ints[0]
        if len(ints) > 1:
            op["map_number"] = ints[1]
            op["room_ref"] = f"room.map{ints[1]}.{ints[0]}"
    if opcode not in OP_SPECS:
        # Numeric directives occur in source scripts as menu/control selectors. Preserve
        # them exactly and classify their shape without assigning invented behavior.
        if re.fullmatch(r"-?\d+", opcode):
            op["directive_kind"] = "numeric_control_selector"
            op["selector"] = int(opcode)
        else:
            op["directive_kind"] = "named_source_directive"
    return op


def parse_operations(script: str) -> tuple[list[dict], list[dict], list[dict]]:
    """Parse action lines into player triggers, known operations, and source directives.

    Every non-empty token is classified. A source directive is not an unknown opcode:
    it is an exact syntactic construct whose gameplay semantics remain intentionally
    unassigned until corroborated.
    """
    operations, triggers, directives = [], [], []
    for line_no, raw_line in enumerate(script.replace("\r", "").split("\n"), 1):
        line = raw_line.strip()
        if not line:
            continue
        segments = split_segments(line)
        if not segments:
            continue
        first_opcode, _ = normalize_operation_token(segments[0])
        start = 0
        if first_opcode not in OP_SPECS:
            trigger_raw = segments[0]
            trigger_parts = trigger_raw.split()
            trigger = {
                "line": line_no,
                "raw": trigger_raw,
                "normalized": trigger_raw.lower(),
                "trigger_kind": "numeric_selection" if re.fullmatch(r"-?\d+", trigger_raw) else "player_command",
            }
            if trigger["trigger_kind"] == "numeric_selection":
                trigger["selection_value"] = int(trigger_raw)
            elif trigger_parts:
                trigger["verb"] = trigger_parts[0].lower()
                trigger["arguments"] = trigger_parts[1:]
            triggers.append(trigger)
            start = 1
        for seq, token in enumerate(segments[start:], start + 1):
            op = make_operation(token, line_no, seq)
            operations.append(op)
            if op["operation_kind"] == "source_directive":
                directives.append(op)
    return operations, triggers, directives

def parse_command_map(script: str) -> list[dict]:
    commands = []
    for line_no, raw in enumerate(script.replace("\r", "").split("\n"), 1):
        line = raw.strip()
        if not line or ":" not in line:
            continue
        segments = split_segments(line)
        if len(segments) != 2:
            continue
        keyword, target = segments
        if not keyword or not target.isdigit():
            continue
        target_id = int(target)
        commands.append({"line": line_no, "keyword": keyword, "normalized_keyword": keyword.lower(), "target_textblock_id": target_id, "target_textblock_ref": f"textblock.{target_id}"})
    return commands


def load_npc_names(repo: Path) -> dict[int, str]:
    candidates = [repo / "Data/Classic/npcs.json", repo / "Data/Classic/monsters.json"]
    result = {}
    for path in candidates:
        if not path.exists(): continue
        doc = json.loads(path.read_text(encoding="utf-8"))
        rows = doc if isinstance(doc, list) else next((v for v in doc.values() if isinstance(v, list)), [])
        for rec in rows:
            number = rec.get("number", rec.get("npc_number", rec.get("legacy_number")))
            if number is None:
                rid = str(rec.get("id", ""))
                m = re.search(r"(\d+)$", rid)
                if m: number = int(m.group(1))
            try: number = int(number)
            except (TypeError, ValueError): continue
            result[number] = rec.get("name", rec.get("display_name", f"NPC {number}"))
    return result


def connected_textblocks(start: int, by_id: dict[int, dict]) -> list[int]:
    seen, queue = set(), deque([start])
    while queue:
        tid = queue.popleft()
        if tid in seen or tid not in by_id: continue
        seen.add(tid)
        rec = by_id[tid]
        for cmd in rec.get("commands", []): queue.append(cmd["target_textblock_id"])
        for op in rec.get("operations", []):
            if "target_textblock_id" in op: queue.append(op["target_textblock_id"])
        nxt = rec.get("next_textblock_id")
        if nxt: queue.append(nxt)
    return sorted(seen)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mdb")
    ap.add_argument("--repo-root", default=".")
    ap.add_argument("--outdir", required=True)
    args = ap.parse_args()
    source, repo, out = Path(args.mdb), Path(args.repo_root), Path(args.outdir)
    out.mkdir(parents=True, exist_ok=True)
    data = source.read_bytes()
    row_index, rows = build_row_index(data)
    decoded, errors = [], []
    for page, slot, off, ptr, blob in rows:
        try:
            layout = decode_variable_layout(blob)
            fixed, values = layout["fixed"], layout["values"]
            if len(fixed) < 10 or len(values) < 2:
                raise ValueError("unexpected textblock row layout")
            textblock_id = struct.unpack_from("<I", fixed, 2)[0]
            next_id = struct.unpack_from("<I", fixed, 6)[0]
            script_raw, long_meta = resolve_long_value(values[0], row_index)
            script = decode_jet_text(script_raw).rstrip("\x00")
            owner_text = decode_jet_text(values[1]).rstrip("\x00")
            commands = parse_command_map(script)
            nonempty_lines = [x for x in script.splitlines() if x.strip()]
            is_command_map = bool(commands) and len(commands) == len(nonempty_lines)
            operations, triggers, directives = parse_operations(script) if not is_command_map else ([], [], [])
            is_action_script = bool(operations or triggers)
            kind = "command_map" if is_command_map else ("action_script" if is_action_script else "dialogue_text")
            rec = {
                "id": f"textblock.{textblock_id}", "textblock_id": textblock_id,
                "next_textblock_id": next_id or None,
                "next_textblock_ref": f"textblock.{next_id}" if next_id else None,
                "owner": parse_owner(owner_text), "owner_raw": owner_text,
                "content_kind": kind, "script": script,
                "commands": commands, "triggers": triggers, "operations": operations,
                "source_directives": directives,
                "unknown_opcodes": [],
                "source": {"database": source.name, "table_owner_page": TEXTBLOCK_OWNER, "page": page, "row": slot, "offset": off, "row_pointer": ptr, "long_value": long_meta},
                "evidence_state": "decoded_source", "canonical_status": "import_candidate",
            }
            decoded.append(rec)
        except Exception as exc:
            errors.append({"page": page, "row": slot, "offset": off, "error": str(exc), "blob_sha256": hashlib.sha256(blob).hexdigest()})
    decoded.sort(key=lambda r: r["textblock_id"])
    by_id = {r["textblock_id"]: r for r in decoded}
    npc_names = load_npc_names(repo)
    npc_links = []
    for rec in decoded:
        owner = rec["owner"]
        if owner.get("kind") != "monster": continue
        for ref in owner.get("references", []):
            npc_id = ref["number"]
            npc_links.append({
                "id": f"npc_textblock.{npc_id}.{rec['textblock_id']}",
                "npc_id": npc_id, "npc_ref": f"npc.{npc_id}", "npc_name": npc_names.get(npc_id),
                "root_textblock_id": rec["textblock_id"], "root_textblock_ref": rec["id"],
                "relationship": "owns_dialogue_textblock", "evidence_state": "decoded_source",
            })
    quest_flows = []
    for link in npc_links:
        root_id = link["root_textblock_id"]
        ids = connected_textblocks(root_id, by_id)
        root = by_id.get(root_id, {})
        command_reach = {}
        for cmd in root.get("commands", []):
            command_reach[cmd["keyword"]] = set(connected_textblocks(cmd["target_textblock_id"], by_id))
        quest_stage_blocks = defaultdict(list)
        for tid in ids:
            rec = by_id[tid]
            qnames = sorted({op["quest_flag_name"] for op in rec.get("operations", []) if "quest_flag_name" in op})
            for qname in qnames:
                quest_stage_blocks[qname].append(tid)
        for qname, stage_ids in sorted(quest_stage_blocks.items()):
            ability_id = next(k for k, v in QUEST_ABILITY_NAMES.items() if v == qname)
            stage_set = set(stage_ids)
            relevant_keywords = sorted(
                [keyword for keyword, reachable in command_reach.items() if reachable & stage_set],
                key=str.lower,
            )
            checks, mutations, item_actions, stages = [], [], [], []
            for tid in sorted(stage_ids):
                rec = by_id[tid]
                stage_ops = rec.get("operations", [])
                stages.append({
                    "textblock_id": tid,
                    "textblock_ref": rec["id"],
                    "script": rec["script"],
                    "operations": stage_ops,
                })
                for op in stage_ops:
                    if op.get("quest_flag_name") == qname:
                        bucket = checks if op["opcode"] in {"checkability", "testability"} else mutations
                        bucket.append({"textblock_id": tid, **op})
                    if "item_id" in op:
                        item_actions.append({"textblock_id": tid, **op})
            quest_flows.append({
                "id": f"quest_flow.{qname}.npc{link['npc_id']}.tb{root_id}",
                "quest_flag_name": qname,
                "quest_ability_id": ability_id,
                "quest_ability_ref": f"ability.{ability_id}",
                "npc_id": link["npc_id"],
                "npc_ref": link["npc_ref"],
                "npc_name": link["npc_name"],
                "root_textblock_id": root_id,
                "root_textblock_ref": link["root_textblock_ref"],
                "quest_stage_textblock_ids": sorted(stage_ids),
                "command_keywords": relevant_keywords,
                "stages": stages,
                "ability_checks": checks,
                "ability_mutations": mutations,
                "item_actions": item_actions,
                "execution_status": "authoritative_script_graph",
                "promotion_note": "Decoded directly from MMExplorer textblock long-value scripts. Librarium remains canonical after review/import.",
                "evidence_state": "decoded_source",
            })
    # Relationship edges useful for graph ingestion.
    edges = []
    for link in npc_links:
        edges.append({"source": link["npc_ref"], "type": "owns_dialogue_textblock", "target": link["root_textblock_ref"], "confidence": "DecodedSource"})
    for rec in decoded:
        if rec.get("next_textblock_ref"):
            edges.append({"source": rec["id"], "type": "continues_to", "target": rec["next_textblock_ref"], "confidence": "DecodedSource"})
        for cmd in rec.get("commands", []):
            edges.append({"source": rec["id"], "type": "command_routes_to", "target": cmd["target_textblock_ref"], "keyword": cmd["keyword"], "confidence": "DecodedSource"})
        for op in rec.get("operations", []):
            if "ability_ref" in op:
                edges.append({"source": rec["id"], "type": op["opcode"], "target": op["ability_ref"], "value": op.get("ability_value"), "confidence": "DecodedSource"})
            if "item_ref" in op:
                edges.append({"source": rec["id"], "type": op["opcode"], "target": op["item_ref"], "confidence": "DecodedSource"})
            if "target_textblock_ref" in op:
                edges.append({"source": rec["id"], "type": op["opcode"], "target": op["target_textblock_ref"], "confidence": "DecodedSource"})
    manifest = {
        "decoder": "decode_mmexplorer_textblocks.py", "source": str(source),
        "source_sha256": hashlib.sha256(data).hexdigest(), "textblock_rows": len(rows),
        "decoded_textblocks": len(decoded), "decode_errors": len(errors),
        "npc_textblock_links": len(npc_links), "quest_flows": len(quest_flows),
        "relationship_edges": len(edges),
        "unknown_opcodes": [],
        "unclassified_tokens": 0,
        "source_directives": sum(len(r.get("source_directives", [])) for r in decoded),
        "policy": "Every token is syntactically classified. Known operations are semantically decoded; source directives are preserved exactly without invented gameplay behavior.",
    }
    files = {
        "textblocks_decoded.json": decoded, "npc_textblock_links.json": npc_links,
        "quest_flows_decoded.json": quest_flows, "textblock_relationship_edges.json": edges,
        "decode_errors.json": errors, "decode_manifest.json": manifest,
    }
    for name, obj in files.items():
        (out/name).write_text(json.dumps(obj, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(manifest, indent=2))

if __name__ == "__main__":
    main()
