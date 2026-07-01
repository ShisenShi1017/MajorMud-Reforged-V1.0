from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from .data import EvidenceStore, SimData
from .formatting import format_rows
from .player import SimulationSession, PlayerState
from .world import WorldEngine, SpawnState
from .util import norm


def build(root: Path | None = None) -> tuple[SimData, WorldEngine]:
    data = SimData(EvidenceStore(root) if root else None)
    return data, WorldEngine(data)


def cmd_summary(data: SimData, world: WorldEngine, args) -> None:
    print("MajorMUD Reforged Simulator")
    for k, v in data.summary().items():
        print(f"{k}: {v}")
    print("\nReadiness:")
    print(format_rows(data.readiness, ["domain", "status", "simulator_scope", "runtime_boundary"], 50))


def cmd_room(data: SimData, world: WorldEngine, args) -> None:
    row = world.room(args.key)
    if not row:
        print(f"No room found for {args.key}")
        return
    print(format_rows([row], ["room_key", "map_id", "room_number", "room_name", "monster_group_id", "group_monster_count", "group_monster_names", "boss_type_id", "boss_name", "room_respawn_minutes", "shop_number", "evidence_basis"], 1))


def cmd_search_room(data: SimData, world: WorldEngine, args) -> None:
    found = world.search_rooms(args.text)
    print(format_rows(found, ["room_key", "map_id", "room_number", "room_name", "monster_group_id", "boss_name", "shop_number"], args.limit))
    print(f"Found {len(found)} room rows")


def cmd_exits(data: SimData, world: WorldEngine, args) -> None:
    found = world.exits(args.code)
    print(format_rows(found, ["from_code", "from_name", "to_code", "to_name", "first_command", "final_command", "classification", "one_way_candidate"], args.limit))


def cmd_path(data: SimData, world: WorldEngine, args) -> None:
    path = world.path(args.start, args.goal)
    if not path:
        print(f"No path found from {args.start} to {args.goal} in extracted navigation graph")
        return
    print(" -> ".join([norm(args.start).upper()] + [norm(e.get("to_code")).upper() for e in path]))
    for e in path:
        print(f"{e.get('from_code')} to {e.get('to_code')}: {e.get('first_command')} ... {e.get('final_command')} ({e.get('classification')})")


def cmd_shop(data: SimData, world: WorldEngine, args) -> None:
    cat, inv = world.shop(args.id)
    if cat:
        print(format_rows([cat], ["shop_legacy_number", "shop_name", "shop_type", "room_name", "map_id", "room_number", "inventory_slots_nonempty"], 1))
    print(format_rows(inv, ["slot", "item_id", "item_name", "item_type", "normal_qty", "max_qty", "regen_time", "availability_type"], args.limit))


def cmd_search_shop(data: SimData, world: WorldEngine, args) -> None:
    found = world.search_shops(args.text)
    print(format_rows(found, ["shop_legacy_number", "shop_name", "slot", "item_id", "item_name", "normal_qty", "max_qty"], args.limit))
    print(f"Found {len(found)} inventory rows")


def cmd_monster(data: SimData, world: WorldEngine, args) -> None:
    row = world.monster(args.id_or_name)
    if not row:
        print(f"No combat row for monster {args.id_or_name}")
        return
    print(format_rows([row], ["monster_id", "name", "level", "hp", "damage_min", "damage_max", "xp", "gold", "combat_fields_present", "runtime_status"], 1))


def cmd_drops(data: SimData, world: WorldEngine, args) -> None:
    currency, drops = world.drops(args.id_or_name)
    if currency:
        print("Currency:")
        print(format_rows([currency], ["monster_id", "monster_name", "copper", "silver", "gold", "platinum", "runic"], 1))
    print("Drops:")
    print(format_rows(drops, ["monster_id", "monster_name", "item_id", "item_name", "drop_percent", "item_type"], args.limit))
    print(f"Drop rows: {len(drops)}")


def cmd_npc(data: SimData, world: WorldEngine, args) -> None:
    found = world.npc(args.text)
    print(format_rows(found, ["source_file", "label", "message_number", "runtime_candidate_category", "text"], args.limit))
    print(f"Found {len(found)} NPC/dialogue/script candidate rows")


def cmd_spawn(data: SimData, world: WorldEngine, args) -> None:
    state = SpawnState(world)
    monsters = state.spawn_room(args.room_key)
    if args.json:
        print(json.dumps(monsters, indent=2))
    else:
        for m in monsters[:args.limit]:
            print(" | ".join(f"{k}={v}" for k, v in m.items()))
        if len(monsters) > args.limit:
            print(f"... {len(monsters)-args.limit} more")
        print(f"Spawn-state rows: {len(monsters)}")


def cmd_combat(data: SimData, world: WorldEngine, args) -> None:
    session = SimulationSession(world)
    result = session.fight_projection(args.id_or_name)
    print(json.dumps(result, indent=2))


def cmd_play(data: SimData, world: WorldEngine, args) -> None:
    session = SimulationSession(world, PlayerState(location=args.start.upper()))
    print("MajorMUD Reforged Simulator interactive shell")
    print("Commands: look, exits, go CODE, path CODE, shop ID, buy ID TEXT, monster TEXT, drops TEXT, fight TEXT, npc TEXT, where, quit")
    while True:
        try:
            line = input(f"[{session.player.location}]> ").strip()
        except EOFError:
            print()
            return
        if not line:
            continue
        parts = line.split()
        cmd = parts[0].lower()
        rest = line[len(parts[0]):].strip()
        if cmd in {"quit", "exit"}:
            return
        if cmd == "where":
            print(session.player)
        elif cmd in {"look", "exits"}:
            print(format_rows(world.exits(session.player.location), ["from_code", "from_name", "to_code", "to_name", "first_command", "final_command", "classification"], 20))
        elif cmd == "go" and rest:
            print("Moved." if session.move_to(rest) else "No extracted edge to that code from here.")
        elif cmd == "path" and rest:
            path = world.path(session.player.location, rest)
            print("No path." if not path else " -> ".join([session.player.location] + [norm(e.get("to_code")).upper() for e in path]))
        elif cmd == "shop" and rest:
            cat, inv = world.shop(rest)
            if cat: print(format_rows([cat], ["shop_legacy_number", "shop_name", "room_name", "inventory_slots_nonempty"], 1))
            print(format_rows(inv, ["slot", "item_id", "item_name", "normal_qty"], 20))
        elif cmd == "buy" and len(parts) >= 3:
            print(session.buy(parts[1], " ".join(parts[2:])))
        elif cmd == "monster" and rest:
            row = world.monster(rest)
            print("No monster." if not row else format_rows([row], ["monster_id", "name", "level", "hp", "damage_min", "damage_max", "xp", "gold"], 1))
        elif cmd == "drops" and rest:
            cur, drops = world.drops(rest)
            if cur: print(format_rows([cur], ["monster_id", "monster_name", "copper", "silver", "gold", "platinum", "runic"], 1))
            print(format_rows(drops, ["monster_name", "item_id", "item_name", "drop_percent"], 20))
        elif cmd == "fight" and rest:
            print(json.dumps(session.fight_projection(rest), indent=2))
        elif cmd == "npc" and rest:
            print(format_rows(world.npc(rest), ["source_file", "label", "message_number", "runtime_candidate_category", "text"], 10))
        else:
            print("Unknown or incomplete command.")


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="MajorMUD Reforged evidence-backed simulator")
    p.add_argument("--root", type=Path, help="Simulator package root containing Evidence/")
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("summary")
    a = sub.add_parser("room"); a.add_argument("key")
    a = sub.add_parser("search-room"); a.add_argument("text"); a.add_argument("--limit", type=int, default=50)
    a = sub.add_parser("exits"); a.add_argument("code"); a.add_argument("--limit", type=int, default=100)
    a = sub.add_parser("path"); a.add_argument("start"); a.add_argument("goal")
    a = sub.add_parser("shop"); a.add_argument("id"); a.add_argument("--limit", type=int, default=200)
    a = sub.add_parser("search-shop"); a.add_argument("text"); a.add_argument("--limit", type=int, default=80)
    a = sub.add_parser("monster"); a.add_argument("id_or_name")
    a = sub.add_parser("drops"); a.add_argument("id_or_name"); a.add_argument("--limit", type=int, default=200)
    a = sub.add_parser("npc"); a.add_argument("text"); a.add_argument("--limit", type=int, default=50)
    a = sub.add_parser("spawn"); a.add_argument("room_key"); a.add_argument("--limit", type=int, default=100); a.add_argument("--json", action="store_true")
    a = sub.add_parser("combat"); a.add_argument("id_or_name")
    a = sub.add_parser("play"); a.add_argument("--start", default="ACCD")
    return p


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    data, world = build(args.root)
    globals()["cmd_" + args.cmd.replace("-", "_")](data, world, args)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
