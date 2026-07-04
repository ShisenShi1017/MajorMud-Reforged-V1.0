#!/usr/bin/env python3
"""
MajorMUD Reforged Evidence-Backed Simulator

This is a local simulator built from RuntimePrep0001-0009 outputs. It is NOT Worldgroup and it does
NOT certify live gameplay equivalence. It provides a playable/queryable expected-model shell for:
rooms, navigation, expected populations, shops, drops, NPC/dialogue candidates, combat placeholders,
and simulator/runtime-validation checklists.
"""
from __future__ import annotations

import argparse
import cmd
import csv
import json
import math
import random
import shlex
import sys
from dataclasses import dataclass, field
from pathlib import Path
from collections import defaultdict, deque
from typing import Dict, Iterable, List, Optional, Tuple

ROOT = Path(__file__).resolve().parent
EVIDENCE = ROOT / "Evidence"
CONFIG_PATH = ROOT / "config" / "simulator_preset.json"
CONFIG = json.loads(CONFIG_PATH.read_text(encoding="utf-8")) if CONFIG_PATH.exists() else {
    "preset": "evidence-backed-default",
    "runtime_boundary": "expected-model-only; live Worldgroup behavior not certified",
}

UNKNOWN_DIR = ROOT / "unknowns"
TRACE_DIR = ROOT / "trace_logs"
TRACE_DIR.mkdir(exist_ok=True)

DIRECTIONS = {
    "n": "north", "north": "north", "s": "south", "south": "south", "e": "east", "east": "east",
    "w": "west", "west": "west", "u": "up", "up": "up", "d": "down", "down": "down",
    "ne": "northeast", "nw": "northwest", "se": "southeast", "sw": "southwest",
    "in": "in", "out": "out", "enter": "enter", "exit": "exit",
}


def norm(value) -> str:
    return "" if value is None else str(value).strip()


def low(value) -> str:
    return norm(value).lower()


def parse_int(value, default: int = 0) -> int:
    s = norm(value)
    if not s:
        return default
    try:
        return int(float(s))
    except ValueError:
        return default


def parse_float(value, default: float = 0.0) -> float:
    s = norm(value)
    if not s:
        return default
    try:
        return float(s)
    except ValueError:
        return default


def csv_rows(pattern: str) -> List[dict]:
    matches = sorted(EVIDENCE.rglob(pattern))
    if not matches:
        return []
    with matches[0].open(newline="", encoding="utf-8-sig", errors="replace") as f:
        return list(csv.DictReader(f))


def contains(row: dict, text: str) -> bool:
    t = text.lower()
    return any(t in str(v).lower() for v in row.values())


def print_table(rows: List[dict], fields: List[str], limit: int = 30) -> None:
    if not rows:
        print("No rows.")
        return
    for row in rows[:limit]:
        print(" | ".join(f"{field}={norm(row.get(field))}" for field in fields if field in row))
    if len(rows) > limit:
        print(f"... {len(rows) - limit} more")


@dataclass
class MobInstance:
    ident: str
    name: str
    source: str
    max_hp: int = 10
    hp: int = 10
    damage_min: int = 1
    damage_max: int = 3
    xp: int = 0
    gold: int = 0
    currency: dict = field(default_factory=dict)
    defeated: bool = False


@dataclass
class PlayerState:
    location_room_key: Optional[str] = "map1-room1"
    location_nav_code: Optional[str] = None
    hp: int = 50
    max_hp: int = 50
    gold: int = 0
    currency: dict = field(default_factory=lambda: {"copper": 0, "silver": 0, "gold": 0, "platinum": 0, "runic": 0})
    xp: int = 0
    inventory: List[dict] = field(default_factory=list)
    room_mobs: List[MobInstance] = field(default_factory=list)
    minutes_elapsed: int = 0
    rng_seed: int = 111
    next_respawn_due_minute: Optional[int] = None
    combat_active: bool = False
    combat_target: Optional[str] = None


class SimData:
    """Loads the evidence-backed RuntimePrep matrices and builds indexes."""
    def __init__(self):
        self.room_rows = csv_rows("RoomPopulationMatrix_RuntimePrep0002.csv")
        self.boss_rows = csv_rows("BossRespawnMatrix_RuntimePrep0002.csv")
        self.spawn_groups = csv_rows("SpawnGroupSummary_RuntimePrep0002.csv")
        self.linked_spawn_rooms = csv_rows("LinkedSpawnRoomMatrix_RuntimePrep0002.csv")
        self.nav_rows = csv_rows("NavigationEdgeMatrix_RuntimePrep0003.csv")
        self.special_nav = csv_rows("SpecialNavigationCandidateMatrix_RuntimePrep0003.csv")
        self.one_way_nav = csv_rows("OneWayExitCandidateMatrix_RuntimePrep0003.csv")
        self.shop_rows = csv_rows("ShopInventoryMatrix_RuntimePrep0004.csv")
        self.shop_catalog = csv_rows("ShopCatalog_RuntimePrep0004.csv")
        self.item_availability = csv_rows("ItemShopAvailabilityMatrix_RuntimePrep0004.csv")
        self.monster_drops = csv_rows("MonsterDropMatrix_RuntimePrep0005.csv")
        self.currency = csv_rows("MonsterCurrencyDropMatrix_RuntimePrep0005.csv")
        self.container_rewards = csv_rows("ContainerOpenRewardMatrix_RuntimePrep0005.csv")
        self.room_drop_availability = csv_rows("RoomDropAvailabilityMatrix_RuntimePrep0005.csv")
        self.npc_candidates = csv_rows("DialogueScriptQuestCandidateMatrix_RuntimePrep0006.csv")
        self.npc_checklist = csv_rows("NPCQuestRuntimeChecklist_RuntimePrep0006.csv")
        self.monster_combat = csv_rows("MonsterCombatExpectationMatrix_RuntimePrep0007.csv")
        self.spells = csv_rows("SpellSystemExpectationMatrix_RuntimePrep0007.csv")
        self.skills = csv_rows("SkillSystemExpectationMatrix_RuntimePrep0007.csv")
        self.classes = csv_rows("ClassProgressionExpectationMatrix_RuntimePrep0007.csv")
        self.equipment = csv_rows("EquipmentStatExpectationMatrix_RuntimePrep0007.csv")
        self.readiness = csv_rows("FinalSimulatorReadinessCertification_RuntimePrep0009.csv")
        self.validation_backlog = csv_rows("RuntimeValidationLaunchBacklog_RuntimePrep0009.csv")

        self.canonical_monsters = self.load_canonical_monsters()
        self.canonical_monster_by_legacy = {norm(m.get("legacyNumber")): m for m in self.canonical_monsters if norm(m.get("legacyNumber"))}
        self.canonical_monster_by_source = {norm(m.get("sourceId")): m for m in self.canonical_monsters if norm(m.get("sourceId"))}
        self.canonical_monster_by_name = {low(m.get("name")): m for m in self.canonical_monsters if norm(m.get("name"))}

        self.room_by_key = {norm(r.get("room_key")): r for r in self.room_rows if norm(r.get("room_key"))}
        self.room_by_number = defaultdict(list)
        for r in self.room_rows:
            self.room_by_number[norm(r.get("room_number"))].append(r)
        self.rooms_by_name = defaultdict(list)
        for r in self.room_rows:
            self.rooms_by_name[low(r.get("room_name"))].append(r)

        self.nav_from = defaultdict(list)
        self.nav_by_code_name = {}
        for r in self.nav_rows:
            src = norm(r.get("from_code")).upper()
            dst = norm(r.get("to_code")).upper()
            if src:
                self.nav_from[src].append(r)
                self.nav_by_code_name[src] = norm(r.get("from_name"))
            if dst:
                self.nav_by_code_name[dst] = norm(r.get("to_name"))

        self.shop_by_id = defaultdict(list)
        for r in self.shop_rows:
            self.shop_by_id[norm(r.get("shop_legacy_number")).replace(".0", "")].append(r)
        self.shop_catalog_by_id = {norm(r.get("shop_legacy_number")).replace(".0", ""): r for r in self.shop_catalog}

        self.combat_by_id = {norm(r.get("monster_id")): r for r in self.monster_combat if norm(r.get("monster_id"))}
        self.combat_by_name = {low(r.get("name")): r for r in self.monster_combat if norm(r.get("name"))}
        self.drop_by_monster = defaultdict(list)
        for r in self.monster_drops:
            self.drop_by_monster[norm(r.get("monster_id"))].append(r)
        self.currency_by_monster = {norm(r.get("monster_id")): r for r in self.currency}

    def load_canonical_monsters(self) -> List[dict]:
        """Load canonical /Data/monsters.json as simulator fallback truth.

        RuntimePrep combat matrices are evidence projections and are incomplete for some
        spawned monsters. Canonical /Data remains the runtime source of truth, so the
        simulator falls back to it only when matrix combat values are missing.
        """
        repo_root = ROOT.parents[2]
        path = repo_root / "Data" / "monsters.json"
        if not path.exists():
            return []
        with path.open(encoding="utf-8") as f:
            data = json.load(f)
        return data if isinstance(data, list) else []

    def canonical_monster_for(self, monster_id: str, name: str = "") -> Optional[dict]:
        mid = norm(monster_id).replace(".0", "")
        if mid in self.canonical_monster_by_legacy:
            return self.canonical_monster_by_legacy[mid]
        if mid in self.canonical_monster_by_source:
            return self.canonical_monster_by_source[mid]
        lname = low(name)
        if lname in self.canonical_monster_by_name:
            return self.canonical_monster_by_name[lname]
        # Spawn groups often carry names with adjectives such as large/fierce. Do
        # not invent a new monster; only use a canonical base when the full name
        # contains the canonical name as a clear token sequence.
        if lname:
            matches = [m for n, m in self.canonical_monster_by_name.items() if n and n in lname]
            if len(matches) == 1:
                return matches[0]
        return None

    def room_label(self, key: str) -> str:
        row = self.room_by_key.get(key)
        return f"{key} — {norm(row.get('room_name'))}" if row else key

    def find_room(self, token: str) -> Optional[dict]:
        token = norm(token)
        if token in self.room_by_key:
            return self.room_by_key[token]
        if token.lower().startswith("map") and "-room" in token.lower():
            return self.room_by_key.get(token.lower()) or self.room_by_key.get(token)
        by_num = self.room_by_number.get(token)
        if by_num:
            return by_num[0]
        exact = self.rooms_by_name.get(token.lower())
        if exact:
            return exact[0]
        for r in self.room_rows:
            if token.lower() in low(r.get("room_name")):
                return r
        return None

    def find_monster_id(self, ident: str) -> str:
        s = norm(ident)
        if s in self.combat_by_id or s in self.drop_by_monster or s in self.currency_by_monster:
            return s
        if s.lower() in self.combat_by_name:
            return norm(self.combat_by_name[s.lower()].get("monster_id"))
        for mid, row in self.combat_by_id.items():
            if s.lower() in low(row.get("name")):
                return mid
        for row in self.monster_drops:
            if s.lower() in low(row.get("monster_name")):
                return norm(row.get("monster_id"))
        return s

    def make_mob(self, monster_id: str, name: str, source: str) -> MobInstance:
        combat = self.combat_by_id.get(norm(monster_id)) or self.combat_by_name.get(low(name))
        canonical = self.canonical_monster_for(monster_id, name)

        # Canonical /Data is the simulator source of truth when a canonical
        # monster can be resolved. RuntimePrep combat matrices remain imported
        # evidence/projections only, so their placeholder values must not
        # override promoted canonical HP, damage, XP, or currency.
        attacks = (canonical or {}).get("attacks") or []
        canonical_mins = [parse_int(a.get("min_hit"), 0) for a in attacks if parse_int(a.get("min_hit"), 0) > 0]
        canonical_maxes = [parse_int(a.get("max_hit"), 0) for a in attacks if parse_int(a.get("max_hit"), 0) > 0]

        if canonical:
            hp = parse_int((canonical.get("stats") or {}).get("hp"), 0)
            dmg_min = min(canonical_mins) if canonical_mins else 0
            dmg_max = max(canonical_maxes) if canonical_maxes else 0
            xp = parse_int(((canonical.get("experience") or {}).get("base")), 0)
            canonical_currency = canonical.get("currencyDrops") or {}
            currency = {coin: parse_int(canonical_currency.get(coin), 0) for coin in ["copper", "silver", "gold", "platinum", "runic"]}
            gold = currency.get("gold", 0)
        else:
            hp = parse_int(combat.get("hp") if combat else None, 0)
            dmg_min = parse_int(combat.get("damage_min") if combat else None, 0)
            dmg_max = parse_int(combat.get("damage_max") if combat else None, 0)
            xp = parse_int(combat.get("xp") if combat else None, 0)
            gold = parse_int(combat.get("gold") if combat else None, 0)
            currency = {"copper": 0, "silver": 0, "gold": gold, "platinum": 0, "runic": 0}

        if hp <= 0:
            hp = max(5, parse_int(combat.get("level") if combat else None, 1) * 5)
        if dmg_min <= 0:
            dmg_min = 1
        if dmg_max <= 0:
            dmg_max = max(dmg_min, 3)

        return MobInstance(norm(monster_id) or name, name or norm(monster_id), source, hp, hp, dmg_min, max(dmg_min, dmg_max), xp, gold, currency)

    def expected_room_mobs(self, room_row: dict) -> List[MobInstance]:
        mobs: List[MobInstance] = []
        ids = [x for x in norm(room_row.get("group_monster_legacy_numbers")).split(";") if x]
        names = [x for x in norm(room_row.get("group_monster_names")).split(";") if x]
        for i, name in enumerate(names):
            mid = ids[i] if i < len(ids) else ""
            mobs.append(self.make_mob(mid, name, "room_population_group"))
        boss_id = norm(room_row.get("boss_type_id"))
        boss_name = norm(room_row.get("boss_name"))
        if boss_id and boss_id not in {"0", "0.0"}:
            mobs.append(self.make_mob(boss_id, boss_name or f"boss-{boss_id}", "room_boss"))
        return mobs

    def search_all(self, text: str) -> Dict[str, List[dict]]:
        return {
            "rooms": [r for r in self.room_rows if contains(r, text)],
            "navigation": [r for r in self.nav_rows if contains(r, text)],
            "shops": [r for r in self.shop_rows if contains(r, text)],
            "drops": [r for r in self.monster_drops if contains(r, text)],
            "npc_dialogue": [r for r in self.npc_candidates if contains(r, text)],
            "spells": [r for r in self.spells if contains(r, text)],
            "skills": [r for r in self.skills if contains(r, text)],
            "equipment": [r for r in self.equipment if contains(r, text)],
        }



class UnknownManager:
    """Tracks unresolved/static-limited/runtime-only evidence without inventing resolutions."""
    def __init__(self):
        self.queue = csv_rows("UNRESOLVED_QUEUE.csv") if UNKNOWN_DIR.exists() else []
        # csv_rows searches Evidence by default; load unknowns directly too.
        direct = UNKNOWN_DIR / "UNRESOLVED_QUEUE.csv"
        if direct.exists():
            with direct.open(newline="", encoding="utf-8-sig", errors="replace") as f:
                self.queue = list(csv.DictReader(f))
        self.trace_events: List[dict] = []

    def log(self, event_type: str, unknown_id: str = "", context: str = "", details: str = "") -> None:
        self.trace_events.append({
            "event_type": event_type,
            "unknown_id": unknown_id,
            "context": context,
            "details": details,
            "handling": "placeholder_or_defer; no inference",
        })

    def list(self, category: str = "", limit: int = 100) -> List[dict]:
        rows = self.queue
        if category:
            rows = [r for r in rows if category.lower() in low(r.get("category")) or category.lower() in low(r.get("description"))]
        return rows[:limit]

    def find(self, text: str, limit: int = 100) -> List[dict]:
        return [r for r in self.queue if contains(r, text)][:limit]

    def placeholder_item(self, item_id: str, context: str) -> dict:
        placeholder = {"item_id": norm(item_id), "item_name": f"UnknownItem:{norm(item_id)}", "source": "unknown_placeholder"}
        self.log("placeholder_item", "", context, f"Created safe placeholder for unresolved item {item_id}")
        return placeholder

    def run_all_unknowns(self) -> List[dict]:
        results=[]
        for r in self.queue:
            uid=norm(r.get("unknown_id"))
            category=norm(r.get("category"))
            desc=norm(r.get("description"))
            if "runtime" in category or "runtime" in low(desc) or "live" in low(desc) or "Worldgroup" in desc:
                outcome="deferred_runtime_required"
            elif "loot" in low(desc) or "item" in low(r.get("linked_entity")):
                outcome="placeholder_item_supported"
            else:
                outcome="placeholder_trace_supported"
            event={"unknown_id":uid,"category":category,"outcome":outcome,"description":desc[:240]}
            results.append(event)
            self.log("simulate_unknown", uid, category, outcome)
        return results

    def export_trace(self, path: Optional[Path] = None) -> Path:
        if path is None:
            path = TRACE_DIR / "unknown_trace_latest.csv"
        with path.open("w", encoding="utf-8", newline="") as f:
            fieldnames=["event_type","unknown_id","context","details","handling"]
            w=csv.DictWriter(f, fieldnames=fieldnames); w.writeheader(); w.writerows(self.trace_events)
        return path

class SimulatorEngine:
    def __init__(self, data: SimData):
        self.data = data
        self.unknowns = UnknownManager()
        self.state = PlayerState()
        self.rng = random.Random(self.state.rng_seed)
        self.enter_room(self.state.location_room_key or "map1-room1")

    def enter_room(self, room_key_or_name: str) -> str:
        row = self.data.find_room(room_key_or_name)
        if not row:
            return f"No room found for {room_key_or_name}"
        self.state.location_room_key = norm(row.get("room_key"))
        self.state.room_mobs = self.data.expected_room_mobs(row)
        self.state.next_respawn_due_minute = None
        self.state.combat_active = False
        self.state.combat_target = None
        return self.look()

    def look(self) -> str:
        row = self.data.room_by_key.get(self.state.location_room_key or "")
        if not row:
            return "You are not in a known RuntimePrep room."
        lines = [f"Room: {norm(row.get('room_key'))} — {norm(row.get('room_name'))}"]
        lines.append(f"Map={norm(row.get('map_id'))} Room#={norm(row.get('room_number'))} RespawnMinutes={norm(row.get('room_respawn_minutes')) or 'unknown'}")
        if norm(row.get("shop_number")) not in {"", "0", "0.0"}:
            lines.append(f"Shop here: {norm(row.get('shop_number')).replace('.0','')}")
        if norm(row.get("monster_group_id")) not in {"", "0", "0.0"}:
            lines.append(f"Monster group: {norm(row.get('monster_group_id'))} ({norm(row.get('group_monster_count'))} configured members)")
        if norm(row.get("boss_type_id")) not in {"", "0", "0.0"}:
            lines.append(f"Boss: {norm(row.get('boss_name'))} (id {norm(row.get('boss_type_id'))}, regen {norm(row.get('boss_regen_time'))})")
        alive = [m for m in self.state.room_mobs if not m.defeated]
        if alive:
            lines.append("Present expected mobs: " + ", ".join(f"{m.name}[{m.hp}/{m.max_hp}]" for m in alive[:20]))
            if len(alive) > 20:
                lines.append(f"... {len(alive)-20} more configured mob entries")
        else:
            lines.append("No currently spawned expected mobs in this simulator state.")
        return "\n".join(lines)

    def nav_enter(self, code: str) -> str:
        code = norm(code).upper()
        if not code:
            return "Missing navigation code."
        self.state.location_nav_code = code
        name = self.data.nav_by_code_name.get(code, "unknown nav node")
        return f"Navigation node: {code} — {name}\n" + self.nav_exits()

    def nav_exits(self) -> str:
        code = norm(self.state.location_nav_code).upper()
        edges = self.data.nav_from.get(code, [])
        if not code:
            return "No navigation node selected. Use: nav <code>"
        if not edges:
            return f"No extracted navigation exits from {code}."
        lines = [f"Exits from {code}:"]
        for e in edges:
            lines.append(f"  {norm(e.get('first_command')) or '?'} -> {norm(e.get('to_code'))} ({norm(e.get('to_name'))}; {norm(e.get('classification'))}; one_way={norm(e.get('one_way_candidate'))})")
        return "\n".join(lines)

    def nav_go(self, direction: str) -> str:
        code = norm(self.state.location_nav_code).upper()
        if not code:
            return "No navigation node selected. Use: nav <code>"
        wanted = norm(direction).lower()
        edges = self.data.nav_from.get(code, [])
        candidates = [e for e in edges if low(e.get("first_command")) == wanted or low(e.get("final_command")) == wanted]
        if not candidates:
            return f"No extracted navigation edge from {code} using command '{direction}'."
        e = candidates[0]
        self.state.location_nav_code = norm(e.get("to_code")).upper()
        return f"Moved {direction}: {norm(e.get('from_code'))} -> {norm(e.get('to_code'))}\n{self.nav_exits()}"

    def tick(self, minutes: int) -> str:
        self.state.minutes_elapsed += max(0, minutes)
        row = self.data.room_by_key.get(self.state.location_room_key or "")
        if not row:
            return f"Advanced {minutes} minutes. No room state available."
        due = self.state.next_respawn_due_minute
        if due is not None and self.state.minutes_elapsed >= due:
            self.state.room_mobs = self.data.expected_room_mobs(row)
            self.state.next_respawn_due_minute = None
            return f"Advanced {minutes} minutes. Expected-model respawn refreshed for this room at {self.state.minutes_elapsed} minutes."
        return f"Advanced {minutes} minutes. Total elapsed: {self.state.minutes_elapsed}. No evidence-backed respawn event fired."

    def attack(self, target_text: str) -> str:
        alive = [m for m in self.state.room_mobs if not m.defeated]
        if not alive:
            return "There are no spawned expected mobs here."
        target_text = low(target_text)
        target = None
        for m in alive:
            if target_text in low(m.name) or target_text == low(m.ident):
                target = m
                break
        if target is None:
            target = alive[0]
        combat_started = False
        if not self.state.combat_active or self.state.combat_target != target.ident:
            self.state.combat_active = True
            self.state.combat_target = target.ident
            combat_started = True
        player_damage = self.rng.randint(3, 8)
        target.hp -= player_damage
        lines = []
        if combat_started:
            lines.append("*Combat Engaged*")
        lines.append(f"You hit {target.name} for {player_damage} damage.")
        if target.hp <= 0:
            target.defeated = True
            self.state.xp += target.xp
            for coin, amount in (target.currency or {}).items():
                self.state.currency[coin] = self.state.currency.get(coin, 0) + amount
            self.state.gold = self.state.currency.get("gold", self.state.gold + target.gold)
            row = self.data.room_by_key.get(self.state.location_room_key or "")
            respawn = parse_int(row.get("room_respawn_minutes") if row else None, 0)
            if respawn > 0 and not any(not mob.defeated for mob in self.state.room_mobs):
                self.state.next_respawn_due_minute = self.state.minutes_elapsed + respawn
            awarded_currency = ", ".join(f"{coin}={amount}" for coin, amount in (target.currency or {}).items() if amount) or "none"
            lines.append(f"{target.name} is defeated. Awarded expected-model XP={target.xp}, currency={awarded_currency}.")
            self.state.combat_active = False
            self.state.combat_target = None
            lines.append("*Combat Off*")
            if self.state.next_respawn_due_minute is not None:
                lines.append(f"Expected-model respawn scheduled for minute {self.state.next_respawn_due_minute}.")
            lines += self.roll_drops(target.ident, target.name)
            lines.append("Note: awards/drop rolls are simulator approximations from static evidence, not live runtime certification.")
            return "\n".join(lines)
        mob_damage = self.rng.randint(target.damage_min, target.damage_max)
        self.state.hp = max(0, self.state.hp - mob_damage)
        lines.append(f"{target.name} hits you for {mob_damage}. HP={self.state.hp}/{self.state.max_hp}")
        if self.state.hp <= 0:
            lines.append("You are defeated in simulator state. Use 'rest' to reset HP.")
        return "\n".join(lines)

    def roll_drops(self, monster_id: str, monster_name: str) -> List[str]:
        out = []
        drops = self.data.drop_by_monster.get(norm(monster_id), [])
        if not drops:
            # try name fallback
            drops = [r for r in self.data.monster_drops if low(r.get("monster_name")) == low(monster_name)]
        for row in drops:
            pct = parse_float(row.get("drop_percent"), 0.0)
            if pct >= 100 or self.rng.random() * 100 < pct:
                item_id = norm(row.get("item_id"))
                item_name = norm(row.get("item_name"))
                if item_id and not item_name:
                    item = self.unknowns.placeholder_item(item_id, f"drop:{monster_id}:{monster_name}")
                    out.append(f"Drop placeholder: {item['item_id']}:{item['item_name']} ({pct:g}%) [unresolved evidence preserved]")
                else:
                    item = {"item_id": item_id, "item_name": item_name, "source": "monster_drop"}
                    out.append(f"Drop: {item['item_id']}:{item['item_name']} ({pct:g}%)")
                self.state.inventory.append(item)
        if not out:
            out.append("No item drops rolled from configured drop matrix.")
        return out

    def shop_list(self, shop_id: Optional[str] = None) -> str:
        if not shop_id:
            row = self.data.room_by_key.get(self.state.location_room_key or "")
            shop_id = norm(row.get("shop_number")).replace(".0", "") if row else ""
        shop_id = norm(shop_id).replace(".0", "")
        if not shop_id or shop_id == "0":
            return "No shop id supplied and current room has no configured shop."
        inv = self.data.shop_by_id.get(shop_id, [])
        cat = self.data.shop_catalog_by_id.get(shop_id)
        lines = []
        if cat:
            lines.append(f"Shop {shop_id}: {norm(cat.get('shop_name'))} at {norm(cat.get('room_name'))}")
        if not inv:
            lines.append(f"No inventory rows for shop {shop_id}.")
            return "\n".join(lines)
        for row in inv[:100]:
            lines.append(f"slot {norm(row.get('slot'))}: {norm(row.get('item_id')).replace('.0','')} {norm(row.get('item_name'))} qty={norm(row.get('normal_qty'))}/{norm(row.get('max_qty'))} regen={norm(row.get('regen_time'))}")
        if len(inv) > 100:
            lines.append(f"... {len(inv)-100} more")
        return "\n".join(lines)

    def buy(self, item_text: str, shop_id: Optional[str] = None) -> str:
        if not shop_id:
            row = self.data.room_by_key.get(self.state.location_room_key or "")
            shop_id = norm(row.get("shop_number")).replace(".0", "") if row else ""
        inv = self.data.shop_by_id.get(norm(shop_id).replace(".0", ""), [])
        if not inv:
            return "No shop inventory available for buy."
        found = None
        for row in inv:
            if item_text.lower() in low(row.get("item_name")) or item_text == norm(row.get("slot")) or item_text == norm(row.get("item_id")).replace(".0", ""):
                found = row
                break
        if not found:
            return f"Item '{item_text}' not found in this shop inventory."
        item = {"item_id": norm(found.get("item_id")).replace(".0", ""), "item_name": norm(found.get("item_name")), "source": "shop_expected_inventory"}
        self.state.inventory.append(item)
        return f"Added expected shop item to inventory: {item['item_id']}:{item['item_name']} (price/economy not certified)."

    def inventory(self) -> str:
        currency = ", ".join(f"{coin}={amount}" for coin, amount in self.state.currency.items() if amount) or "none"
        lines = [f"HP {self.state.hp}/{self.state.max_hp} | XP {self.state.xp} | Currency {currency}"]
        if not self.state.inventory:
            lines.append("Inventory is empty.")
        else:
            for i, item in enumerate(self.state.inventory, 1):
                lines.append(f"{i}. {item.get('item_id')}:{item.get('item_name')} [{item.get('source')}]")
        return "\n".join(lines)

    def rest(self) -> str:
        self.state.hp = self.state.max_hp
        return f"Rested. HP={self.state.hp}/{self.state.max_hp}."


class SimShell(cmd.Cmd):
    intro = "MajorMUD Reforged evidence-backed simulator. Type 'help' or 'summary'."
    prompt = "mmud> "

    def __init__(self, engine: SimulatorEngine):
        super().__init__()
        self.engine = engine
        self.data = engine.data

    def do_summary(self, arg):
        command_summary(self.data)

    def do_look(self, arg):
        print(self.engine.look())

    def do_enter(self, arg):
        print(self.engine.enter_room(arg.strip()))

    def do_room(self, arg):
        command_room(self.data, arg.strip())

    def do_search_room(self, arg):
        command_search_room(self.data, arg.strip())

    def do_nav(self, arg):
        print(self.engine.nav_enter(arg.strip()))

    def do_exits(self, arg):
        if arg.strip():
            self.engine.state.location_nav_code = arg.strip().upper()
        print(self.engine.nav_exits())

    def do_go(self, arg):
        print(self.engine.nav_go(arg.strip()))

    def do_path(self, arg):
        parts = shlex.split(arg)
        if len(parts) != 2:
            print("Usage: path START_CODE GOAL_CODE")
            return
        command_path(self.data, parts[0], parts[1])

    def do_tick(self, arg):
        print(self.engine.tick(parse_int(arg, 1)))

    def do_mobs(self, arg):
        alive = [m for m in self.engine.state.room_mobs if not m.defeated]
        if not alive:
            print("No spawned expected mobs here.")
        for m in alive:
            print(f"{m.ident}: {m.name} hp={m.hp}/{m.max_hp} source={m.source}")

    def do_attack(self, arg):
        print(self.engine.attack(arg.strip()))

    def do_rest(self, arg):
        print(self.engine.rest())

    def do_shop(self, arg):
        print(self.engine.shop_list(arg.strip() or None))

    def do_buy(self, arg):
        parts = shlex.split(arg)
        if not parts:
            print("Usage: buy ITEM_TEXT [SHOP_ID]")
            return
        print(self.engine.buy(parts[0], parts[1] if len(parts) > 1 else None))

    def do_inventory(self, arg):
        print(self.engine.inventory())

    def do_search(self, arg):
        command_search_all(self.data, arg.strip())

    def do_monster(self, arg):
        command_monster(self.data, arg.strip())

    def do_drops(self, arg):
        command_drops(self.data, arg.strip())

    def do_npc(self, arg):
        command_npc(self.data, arg.strip())

    def do_spell(self, arg):
        command_spell(self.data, arg.strip())

    def do_skill(self, arg):
        command_skill(self.data, arg.strip())

    def do_unknowns(self, arg):
        rows = self.engine.unknowns.list(arg.strip(), 200)
        print_table(rows, ["unknown_id", "category", "source", "record_id", "severity", "status", "linked_entity", "description"], 200)
        print(f"Unknown rows shown: {len(rows)}")

    def do_find_unknown(self, arg):
        rows = self.engine.unknowns.find(arg.strip(), 200)
        print_table(rows, ["unknown_id", "category", "source", "record_id", "severity", "status", "linked_entity", "description"], 200)
        print(f"Unknown rows found: {len(rows)}")

    def do_simulate_unknowns(self, arg):
        results = self.engine.unknowns.run_all_unknowns()
        print_table(results, ["unknown_id", "category", "outcome", "description"], 300)
        path = self.engine.unknowns.export_trace()
        print(f"Trace exported: {path}")

    def do_trace(self, arg):
        path = self.engine.unknowns.export_trace()
        print(f"Trace exported: {path}")

    def do_backlog(self, arg):
        print_table(self.data.validation_backlog, list(self.data.validation_backlog[0].keys()) if self.data.validation_backlog else [], 50)

    def do_help(self, arg):
        if arg:
            return super().do_help(arg)
        print("Commands:")
        print("  summary | look | enter <room_key/name/number> | room <room_key/name/number> | search_room <text>")
        print("  nav <code> | exits [code] | go <command> | path <start_code> <goal_code>")
        print("  mobs | attack <mob name/id> | tick <minutes> | rest")
        print("  shop [id] | buy <item text|slot|id> [shop_id] | inventory")
        print("  search <text> | monster <id/name> | drops <id/name> | npc <text> | spell <text> | skill <text>")
        print("  unknowns [filter] | find_unknown <text> | simulate_unknowns | trace")
        print("  backlog | quit")
        print("Boundary: expected-model-only; live Worldgroup behavior is not certified.")

    def do_quit(self, arg):
        return True

    def do_exit(self, arg):
        return True

    def emptyline(self):
        pass


def command_summary(data: SimData):
    print("MajorMUD Reforged Evidence-Backed Simulator")
    print(f"Preset: {CONFIG.get('preset', 'unknown')}")
    print(f"Runtime boundary: {CONFIG.get('runtime_boundary', 'expected-model-only')}")
    print(f"Rooms: {len(data.room_rows)} | Nav edges: {len(data.nav_rows)} | Shops: {len(data.shop_catalog)} | Shop items: {len(data.shop_rows)}")
    print(f"Monster drop rows: {len(data.monster_drops)} | Currency rows: {len(data.currency)} | NPC/dialogue candidates: {len(data.npc_candidates)}")
    print(f"Combat rows: {len(data.monster_combat)} | Spells: {len(data.spells)} | Skills: {len(data.skills)} | Classes: {len(data.classes)} | Equipment: {len(data.equipment)}")
    unknown_count = len(UnknownManager().queue)
    print(f"Unresolved/unknown queue rows: {unknown_count}")
    if data.readiness:
        print("\nSimulator readiness:")
        fields = list(data.readiness[0].keys())[:5]
        print_table(data.readiness, fields, 20)


def command_room(data: SimData, token: str):
    row = data.find_room(token)
    if not row:
        print(f"No room found for {token}")
        return
    fields = ["room_key", "map_id", "room_number", "room_name", "monster_group_id", "group_monster_count", "group_monster_names", "boss_type_id", "boss_name", "room_respawn_minutes", "shop_number", "evidence_basis"]
    print_table([row], fields, 1)


def command_search_room(data: SimData, text: str):
    found = [r for r in data.room_rows if contains(r, text)]
    print_table(found, ["room_key", "map_id", "room_number", "room_name", "monster_group_id", "boss_name", "shop_number"], 80)
    print(f"Found {len(found)} room rows")


def command_exits(data: SimData, code: str):
    found = data.nav_from.get(norm(code).upper(), [])
    print_table(found, ["from_code", "from_name", "to_code", "to_name", "first_command", "final_command", "classification", "one_way_candidate"], 100)


def command_path(data: SimData, start: str, goal: str):
    start = norm(start).upper(); goal = norm(goal).upper()
    q = deque([(start, [])]); seen = {start}
    while q:
        cur, path = q.popleft()
        if cur == goal:
            print(" -> ".join([start] + [norm(edge.get("to_code")) for edge in path]))
            for e in path:
                print(f"{norm(e.get('from_code'))} to {norm(e.get('to_code'))}: {norm(e.get('first_command'))} ... {norm(e.get('final_command'))} ({norm(e.get('classification'))})")
            return
        for e in data.nav_from.get(cur, []):
            nxt = norm(e.get("to_code")).upper()
            if nxt and nxt not in seen:
                seen.add(nxt)
                q.append((nxt, path + [e]))
    print(f"No path found from {start} to {goal} in extracted navigation graph")


def command_shop(data: SimData, sid: str):
    engine = SimulatorEngine(data)
    print(engine.shop_list(sid))


def command_search_shop(data: SimData, text: str):
    found = [r for r in data.shop_rows if contains(r, text)]
    print_table(found, ["shop_legacy_number", "shop_name", "slot", "item_id", "item_name", "normal_qty", "max_qty"], 100)
    print(f"Found {len(found)} inventory rows")


def command_monster(data: SimData, ident: str):
    mid = data.find_monster_id(ident)
    row = data.combat_by_id.get(mid)
    if row:
        print_table([row], ["monster_id", "name", "level", "hp", "damage_min", "damage_max", "xp", "gold", "runtime_status"], 1)
    else:
        drops = [r for r in data.monster_drops if mid == norm(r.get("monster_id")) or ident.lower() in low(r.get("monster_name"))]
        if drops:
            print(f"No combat row found, but drop evidence exists for monster {norm(drops[0].get('monster_id'))}:{norm(drops[0].get('monster_name'))}")
        else:
            print(f"No monster evidence found for {ident}")


def command_drops(data: SimData, ident: str):
    mid = data.find_monster_id(ident)
    cur = data.currency_by_monster.get(mid)
    if cur:
        print("Currency:")
        print_table([cur], ["monster_id", "monster_name", "copper", "silver", "gold", "platinum", "runic"], 1)
    drops = data.drop_by_monster.get(mid, [])
    if not drops:
        drops = [r for r in data.monster_drops if ident.lower() in low(r.get("monster_name"))]
    print("Drops:")
    print_table(drops, ["monster_id", "monster_name", "item_id", "item_name", "drop_percent", "item_type"], 100)
    print(f"Drop rows: {len(drops)}")


def command_npc(data: SimData, text: str):
    found = [r for r in data.npc_candidates if contains(r, text)]
    print_table(found, ["source_file", "label", "message_number", "runtime_candidate_category", "text"], 80)
    print(f"Found {len(found)} NPC/dialogue/script candidate rows")


def command_spell(data: SimData, text: str):
    found = [r for r in data.spells if contains(r, text)]
    print_table(found, ["spell_id", "source_id", "name", "command", "notes", "runtime_status"], 80)
    print(f"Found {len(found)} spell rows")


def command_skill(data: SimData, text: str):
    found = [r for r in data.skills if contains(r, text)]
    print_table(found, ["skill_key", "name", "category", "max", "governing_attributes", "effects", "runtime_status"], 80)
    print(f"Found {len(found)} skill rows")


def command_search_all(data: SimData, text: str):
    results = data.search_all(text)
    for domain, rows in results.items():
        print(f"\n[{domain}] {len(rows)} hits")
        if rows:
            print_table(rows, list(rows[0].keys())[:6], 5)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="MajorMUD Reforged evidence-backed simulator")
    sub = parser.add_subparsers(dest="cmd", required=True)
    sub.add_parser("summary")
    sub.add_parser("play")
    a = sub.add_parser("room"); a.add_argument("key")
    a = sub.add_parser("search-room"); a.add_argument("text")
    a = sub.add_parser("exits"); a.add_argument("code")
    a = sub.add_parser("path"); a.add_argument("start"); a.add_argument("goal")
    a = sub.add_parser("shop"); a.add_argument("id")
    a = sub.add_parser("search-shop"); a.add_argument("text")
    a = sub.add_parser("monster"); a.add_argument("id_or_name")
    a = sub.add_parser("drops"); a.add_argument("id_or_name")
    a = sub.add_parser("npc"); a.add_argument("text")
    a = sub.add_parser("spell"); a.add_argument("text")
    a = sub.add_parser("skill"); a.add_argument("text")
    a = sub.add_parser("search"); a.add_argument("text")
    a = sub.add_parser("simulate-room"); a.add_argument("room"); a.add_argument("--ticks", type=int, default=0)
    a = sub.add_parser("unknowns"); a.add_argument("filter", nargs="?", default="")
    a = sub.add_parser("find-unknown"); a.add_argument("text")
    sub.add_parser("simulate-unknowns")
    sub.add_parser("export-trace")
    return parser


def main(argv: Optional[List[str]] = None):
    args = build_parser().parse_args(argv)
    data = SimData()
    if args.cmd == "summary":
        command_summary(data)
    elif args.cmd == "play":
        SimShell(SimulatorEngine(data)).cmdloop()
    elif args.cmd == "room":
        command_room(data, args.key)
    elif args.cmd == "search-room":
        command_search_room(data, args.text)
    elif args.cmd == "exits":
        command_exits(data, args.code)
    elif args.cmd == "path":
        command_path(data, args.start, args.goal)
    elif args.cmd == "shop":
        command_shop(data, args.id)
    elif args.cmd == "search-shop":
        command_search_shop(data, args.text)
    elif args.cmd == "monster":
        command_monster(data, args.id_or_name)
    elif args.cmd == "drops":
        command_drops(data, args.id_or_name)
    elif args.cmd == "npc":
        command_npc(data, args.text)
    elif args.cmd == "spell":
        command_spell(data, args.text)
    elif args.cmd == "skill":
        command_skill(data, args.text)
    elif args.cmd == "search":
        command_search_all(data, args.text)
    elif args.cmd == "simulate-room":
        engine = SimulatorEngine(data)
        print(engine.enter_room(args.room))
        if args.ticks:
            print(engine.tick(args.ticks))
    elif args.cmd == "unknowns":
        um = UnknownManager()
        rows = um.list(args.filter, 300)
        print_table(rows, ["unknown_id", "category", "source", "record_id", "severity", "status", "linked_entity", "description"], 300)
        print(f"Unknown rows shown: {len(rows)}")
    elif args.cmd == "find-unknown":
        um = UnknownManager()
        rows = um.find(args.text, 300)
        print_table(rows, ["unknown_id", "category", "source", "record_id", "severity", "status", "linked_entity", "description"], 300)
        print(f"Unknown rows found: {len(rows)}")
    elif args.cmd == "simulate-unknowns":
        um = UnknownManager()
        results = um.run_all_unknowns()
        print_table(results, ["unknown_id", "category", "outcome", "description"], 300)
        path = um.export_trace()
        print(f"Trace exported: {path}")
    elif args.cmd == "export-trace":
        um = UnknownManager()
        path = um.export_trace()
        print(f"Trace exported: {path}")
    else:
        raise SystemExit(f"Unhandled command {args.cmd}")


if __name__ == "__main__":
    main()
