from __future__ import annotations

from collections import deque
from .data import SimData
from .util import norm, as_int, as_float, contains


class WorldEngine:
    """Expected-model world engine.

    Supports navigation graph traversal, room inspection, population lookup, shops,
    drops, and simple deterministic spawn-state bookkeeping. It intentionally does
    not execute hidden Worldgroup scripts or certify live behavior.
    """

    def __init__(self, data: SimData):
        self.data = data

    def room(self, key: str) -> dict[str, str] | None:
        return self.data.resolve_room(key)

    def search_rooms(self, text: str) -> list[dict[str, str]]:
        return [r for r in self.data.room_rows if contains(r, text)]

    def exits(self, code: str) -> list[dict[str, str]]:
        return self.data.nav_from.get(norm(code).upper(), [])

    def path(self, start: str, goal: str, max_depth: int = 1000) -> list[dict[str, str]] | None:
        start = norm(start).upper()
        goal = norm(goal).upper()
        q = deque([(start, [])])
        seen = {start}
        while q:
            cur, path = q.popleft()
            if cur == goal:
                return path
            if len(path) >= max_depth:
                continue
            for edge in self.exits(cur):
                nxt = norm(edge.get("to_code")).upper()
                if nxt and nxt not in seen:
                    seen.add(nxt)
                    q.append((nxt, path + [edge]))
        return None

    def shop(self, sid: str) -> tuple[dict[str, str] | None, list[dict[str, str]]]:
        key = norm(sid).split(".")[0]
        return self.data.shop_catalog_by_id.get(key), list(self.data.shop_by_id.get(key, []))

    def search_shops(self, text: str) -> list[dict[str, str]]:
        return [r for r in self.data.shop_rows if contains(r, text)]

    def monster(self, ident: str) -> dict[str, str] | None:
        mid = self.data.find_monster_id(ident)
        return self.data.combat_by_id.get(mid)

    def drops(self, ident: str) -> tuple[dict[str, str] | None, list[dict[str, str]]]:
        mid = self.data.find_monster_id(ident)
        return self.data.currency_by_monster.get(mid), list(self.data.drop_by_monster.get(mid, []))

    def npc(self, text: str) -> list[dict[str, str]]:
        return [r for r in self.data.npc_candidates if contains(r, text)]


class SpawnState:
    """Simple expected spawn-state projection from room population rows.

    Room rows provide configured population and respawn fields. This class turns that
    into a deterministic state list for simulator testing. It does not prove actual
    spawn timing, lair behavior, or Worldgroup behavior.
    """

    def __init__(self, world: WorldEngine):
        self.world = world
        self.room_monsters: dict[str, list[dict[str, object]]] = {}

    def spawn_room(self, room_key: str) -> list[dict[str, object]]:
        room = self.world.room(room_key)
        if not room:
            return []
        key = norm(room.get("room_key")) or room_key
        if key in self.room_monsters:
            return self.room_monsters[key]
        spawned: list[dict[str, object]] = []
        ids = [x.strip() for x in norm(room.get("group_monster_legacy_numbers")).split(";") if x.strip()]
        names = [x.strip() for x in norm(room.get("group_monster_names")).split(";") if x.strip()]
        for i, mid in enumerate(ids):
            spawned.append({
                "kind": "group",
                "monster_id": mid,
                "name": names[i] if i < len(names) else mid,
                "alive": True,
                "respawn_minutes": as_int(room.get("room_respawn_minutes"), 0),
            })
        boss_id = norm(room.get("boss_type_id"))
        if boss_id and boss_id not in {"0", "0.0"}:
            spawned.append({
                "kind": "boss",
                "monster_id": boss_id,
                "name": norm(room.get("boss_name")) or boss_id,
                "alive": True,
                "respawn_hours": as_float(room.get("boss_respawn_hours"), 0.0),
                "regen_time": norm(room.get("boss_regen_time")),
            })
        self.room_monsters[key] = spawned
        return spawned

    def defeat(self, room_key: str, monster_id_or_name: str) -> bool:
        monsters = self.spawn_room(room_key)
        target = norm(monster_id_or_name).lower()
        for m in monsters:
            if m.get("alive") and (norm(m.get("monster_id")).lower() == target or target in norm(m.get("name")).lower()):
                m["alive"] = False
                return True
        return False

    def respawn_all(self, room_key: str) -> list[dict[str, object]]:
        monsters = self.spawn_room(room_key)
        for m in monsters:
            m["alive"] = True
        return monsters
