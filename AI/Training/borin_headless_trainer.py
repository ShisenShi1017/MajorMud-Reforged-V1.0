#!/usr/bin/env python3
"""Borin headless reinforcement-learning trainer for MajorMUD Reforged.

This trainer talks only to Reforged.Server's newline-delimited JSON stdin/stdout
headless protocol. It uses tabular Q-learning so it has no third-party Python
runtime dependency and can run unattended for the full 22-hour curriculum.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import random
import re
import signal
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

ANSI = re.compile(r"\x1b\[[0-9;]*m")


def clean(text: str) -> str:
    return ANSI.sub("", text or "")


def read_json(path: Path, fallback: Any) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return fallback


def write_json_atomic(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(value, indent=2, sort_keys=True), encoding="utf-8")
    tmp.replace(path)


class HeadlessClient:
    def __init__(self, repo: Path, player: str, log_path: Path):
        project = repo / "Apps" / "Source" / "Reforged.Server" / "Reforged.Server.csproj"
        if not project.exists():
            raise FileNotFoundError(f"Reforged.Server.csproj not found: {project}")
        self.log = log_path.open("a", encoding="utf-8", buffering=1)
        cmd = [
            "dotnet", "run", "--project", str(project), "--",
            str(repo), "--headless-json", "--player", player,
        ]
        self.proc = subprocess.Popen(
            cmd,
            cwd=str(repo),
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=self.log,
            text=True,
            bufsize=1,
        )
        self.state = self._read_state()

    def _read_state(self) -> dict[str, Any]:
        assert self.proc.stdout is not None
        while True:
            line = self.proc.stdout.readline()
            if line == "":
                raise RuntimeError(f"Headless server exited unexpectedly with code {self.proc.poll()}")
            line = line.strip()
            if not line:
                continue
            try:
                value = json.loads(line)
                if isinstance(value, dict) and value.get("protocol") == "borin-headless-v1":
                    return value
            except json.JSONDecodeError:
                self.log.write("NONJSON STDOUT: " + line + "\n")

    def send(self, command: str) -> dict[str, Any]:
        if self.proc.poll() is not None:
            raise RuntimeError(f"Headless server is not running (exit={self.proc.returncode})")
        assert self.proc.stdin is not None
        self.proc.stdin.write(json.dumps({"operation": "command", "command": command}) + "\n")
        self.proc.stdin.flush()
        self.state = self._read_state()
        return self.state

    def reset_session(self) -> dict[str, Any]:
        assert self.proc.stdin is not None
        self.proc.stdin.write(json.dumps({"operation": "reset"}) + "\n")
        self.proc.stdin.flush()
        self.state = self._read_state()
        return self.state

    def close(self) -> None:
        try:
            if self.proc.poll() is None and self.proc.stdin is not None:
                self.proc.stdin.write(json.dumps({"operation": "shutdown"}) + "\n")
                self.proc.stdin.flush()
                self.proc.wait(timeout=5)
        except Exception:
            try:
                self.proc.kill()
            except Exception:
                pass
        try:
            self.log.close()
        except Exception:
            pass


@dataclass
class Snapshot:
    hp: int = 0
    hp_max: int = 0
    mana: int = 0
    mana_max: int = 0
    wealth_copper: int = 0
    exp: int = 0
    armor_a: int = 0
    armor_b: int = 0
    room: str = ""
    dead: bool = False


def numeric_snapshot(state: dict[str, Any]) -> Snapshot:
    score = clean(str(state.get("score", "")))
    wealth = clean(str(state.get("wealth", "")))
    response = clean(str(state.get("response", ""))).lower()
    hp = hp_max = mana = mana_max = exp = armor_a = armor_b = 0
    m = re.search(r"Hits:\s*(\d+)\s*/\s*(\d+)", score, re.I)
    if m:
        hp, hp_max = map(int, m.groups())
    m = re.search(r"(?:Mana|Kai):\s*\*?\s*(\d+)\s*/\s*(\d+)", score, re.I)
    if m:
        mana, mana_max = map(int, m.groups())
    m = re.search(r"Exp:\s*(\d+)", score, re.I)
    if m:
        exp = int(m.group(1))
    m = re.search(r"Armour Class:\s*(-?\d+)\s*/\s*(-?\d+)", score, re.I)
    if m:
        armor_a, armor_b = map(int, m.groups())
    m = re.search(r"Wealth:\s*(\d+)\s+copper", wealth, re.I)
    copper = int(m.group(1)) if m else 0
    room = str((state.get("room") or {}).get("id", ""))
    dead = hp_max > 0 and hp <= 0 or any(x in response for x in ("you have died", "you are dead", "you die"))
    return Snapshot(hp, hp_max, mana, mana_max, copper, exp, armor_a, armor_b, room, dead)


def gear_metric(s: Snapshot) -> float:
    # Higher survivability and defense is treated as an upgrade signal. Combat reward
    # still teaches Borin when a lower-defense item performs better in practice.
    return s.hp_max * 1.0 + s.armor_a * 4.0 + s.armor_b * 4.0 + s.mana_max * 0.15


def parse_carried_denominations(text: str) -> dict[str, int]:
    text = clean(text)
    carried = text.split("Carried:", 1)[1].split("\n", 1)[0] if "Carried:" in text else ""
    patterns = {
        "runic": r"(\d+)\s+runic",
        "platinum": r"(\d+)\s+platinum",
        "gold": r"(\d+)\s+gold",
        "silver": r"(\d+)\s+silver",
        "copper": r"(\d+)\s+copper",
    }
    out = {}
    for name, pat in patterns.items():
        m = re.search(pat, carried, re.I)
        out[name] = int(m.group(1)) if m else 0
    return out


def inventory_items(text: str) -> list[str]:
    text = clean(text)
    m = re.search(r"You are carrying (.+?)(?:\n|$)", text, re.I)
    if not m or "nothing" in m.group(1).lower():
        return []
    parts = [x.strip().rstrip(".") for x in m.group(1).split(",")]
    result = []
    for part in parts:
        low = part.lower()
        if any(word in low for word in ("runic", "platinum", "gold crown", "silver noble", "copper farthing")):
            continue
        # Do not sell currently equipped gear. It can still be evaluated/replaced first.
        if "(" in part and ")" in part:
            continue
        part = re.sub(r"^\d+\s+", "", part).strip()
        if part:
            result.append(part)
    return result


def shop_items(text: str) -> list[tuple[str, int]]:
    rows = []
    for line in clean(text).splitlines():
        m = re.match(r"^(.{1,30}?)\s{2,}(\d+)\s+(Free|\d+\s+gold crowns?)\s*$", line, re.I)
        if not m:
            continue
        name = m.group(1).strip()
        price = 0 if m.group(3).lower() == "free" else int(re.search(r"\d+", m.group(3)).group(0))
        if int(m.group(2)) > 0:
            rows.append((name, price))
    return rows


class QLearner:
    def __init__(self, path: Path):
        self.path = path
        data = read_json(path, {})
        self.q: dict[str, list[float]] = {k: [float(x) for x in v] for k, v in data.get("q", {}).items()}
        self.gear: dict[str, float] = {k: float(v) for k, v in data.get("gear", {}).items()}
        self.visits: dict[str, int] = {k: int(v) for k, v in data.get("visits", {}).items()}
        self.steps = int(data.get("steps", 0))
        self.total_reward = float(data.get("totalReward", 0.0))
        self.epsilon = float(data.get("epsilon", 0.30))
        self.alpha = 0.18
        self.gamma = 0.94

    def values(self, key: str, n: int) -> list[float]:
        v = self.q.setdefault(key, [0.0] * n)
        if len(v) < n:
            v.extend([0.0] * (n - len(v)))
        elif len(v) > n:
            del v[n:]
        return v

    def choose(self, key: str, n: int) -> int:
        values = self.values(key, n)
        if random.random() < self.epsilon:
            return random.randrange(n)
        best = max(values)
        choices = [i for i, v in enumerate(values) if v == best]
        return random.choice(choices)

    def update(self, key: str, action: int, reward: float, next_key: str, n: int) -> None:
        values = self.values(key, n)
        next_values = self.values(next_key, n)
        values[action] += self.alpha * (reward + self.gamma * max(next_values) - values[action])
        self.steps += 1
        self.total_reward += reward
        self.epsilon = max(0.04, self.epsilon * 0.99997)

    def save(self) -> None:
        write_json_atomic(self.path, {
            "q": self.q,
            "gear": self.gear,
            "visits": self.visits,
            "steps": self.steps,
            "totalReward": self.total_reward,
            "epsilon": self.epsilon,
            "updatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        })


ACTIONS = [
    "look", "score", "inventory", "search", "explore", "get_all", "attack", "flee",
    "rest", "meditate", "services", "list", "bank", "deposit_surplus", "buy_candidate",
    "sell_candidate", "equip_candidate"
]


def state_key(phase: int, state: dict[str, Any], snap: Snapshot) -> str:
    room = state.get("room") or {}
    hp_bucket = 0 if snap.hp_max <= 0 else min(4, int((snap.hp / max(1, snap.hp_max)) * 4))
    wealth_bucket = min(9, snap.wealth_copper // 1000)
    npc = 1 if room.get("visibleNpcs") else 0
    services = 0 if "no generated services" in clean(str(state.get("services", ""))).lower() else 1
    combat = 0 if "not in combat" in clean(str(state.get("combat", ""))).lower() else 1
    return f"{phase}|{room.get('id','')}|h{hp_bucket}|w{wealth_bucket}|n{npc}|s{services}|c{combat}"


def choose_exit(state: dict[str, Any], learner: QLearner) -> str:
    room = state.get("room") or {}
    exits = [str(x) for x in room.get("exits", []) if str(x).strip()]
    if not exits:
        return "look"
    random.shuffle(exits)
    # Bias toward exploration; the Q table decides when exploration itself is worthwhile.
    return exits[0]


def reserve_deposit_command(state: dict[str, Any], reserve_gold: int) -> str:
    bank_text = clean(str(state.get("bank", "")))
    if "unavailable:" in bank_text.lower() or "no generated bank" in bank_text.lower():
        return "look"
    denoms = parse_carried_denominations(bank_text)
    values = {"runic": 1_000_000, "platinum": 10_000, "gold": 100, "silver": 10, "copper": 1}
    total = sum(denoms[k] * values[k] for k in values)
    reserve = reserve_gold * 100
    surplus = max(0, total - reserve)
    if surplus <= 0:
        return "look"
    # Deposit the largest actual denomination that can be removed while preserving reserve.
    for name in ("runic", "platinum", "gold", "silver", "copper"):
        unit = values[name]
        amount = min(denoms[name], surplus // unit)
        if amount > 0:
            return f"deposit {amount} {name}"
    return "look"


def command_for(action: str, phase: int, state: dict[str, Any], learner: QLearner,
                reserve_gold: int, last_shop: list[tuple[str, int]]) -> tuple[str, str | None]:
    snap = numeric_snapshot(state)
    if action == "explore":
        return choose_exit(state, learner), None
    if action == "get_all":
        return "get all", None
    if action == "attack":
        npcs = (state.get("room") or {}).get("visibleNpcs") or []
        if npcs:
            name = str(npcs[0].get("displayName") or npcs[0].get("DisplayName") or "").strip()
            if name:
                return f"attack {name}", None
        return "look", None
    if action == "deposit_surplus":
        return reserve_deposit_command(state, reserve_gold), None
    if action == "buy_candidate":
        # The 10-gold reserve is a hard rule: buying pauses while below reserve.
        if snap.wealth_copper < reserve_gold * 100:
            return "look", None
        affordable = [(name, price) for name, price in last_shop if snap.wealth_copper - price * 100 >= reserve_gold * 100]
        if not affordable:
            return "list", None
        # Prefer items that have previously improved Borin, but keep exploration alive.
        if random.random() < learner.epsilon:
            name, _ = random.choice(affordable)
        else:
            name, _ = max(affordable, key=lambda x: learner.gear.get(x[0].lower(), 0.0) - x[1] * 0.1)
        return f"buy {name}", name
    if action == "sell_candidate":
        items = inventory_items(str(state.get("inventory", "")))
        if not items:
            return "look", None
        name = min(items, key=lambda x: learner.gear.get(x.lower(), 0.0))
        return f"sell {name}", name
    if action == "equip_candidate":
        items = inventory_items(str(state.get("inventory", "")))
        if not items:
            return "look", None
        if random.random() < learner.epsilon:
            name = random.choice(items)
        else:
            name = max(items, key=lambda x: learner.gear.get(x.lower(), 0.0))
        return f"equip {name}", name
    return action, None


def reward_for(phase: int, before: Snapshot, after: Snapshot, command: str, response: str,
               new_room: bool, gear_name: str | None, learner: QLearner, reserve_gold: int,
               elapsed_since_survival_bonus: float) -> tuple[float, bool]:
    text = clean(response).lower()
    reward = 0.0
    survival_bonus_due = elapsed_since_survival_bonus >= 30.0
    if after.dead:
        reward -= 1000.0
    hp_lost = max(0, before.hp - after.hp)
    reward -= float(hp_lost)

    if phase == 0:  # Basic survival
        if survival_bonus_due and not after.dead:
            reward += 10.0
        if command.startswith("flee") and before.hp_max and before.hp / max(1, before.hp_max) < 0.25 and "cannot" not in text:
            reward += 5.0
    elif phase == 1:  # Combat optimization
        if any(x in text for x in ("you kill", "you have slain", "you slay", "is dead", "defeated")):
            reward += 100.0
        reward -= max(0, before.mana - after.mana)
        if any(x in text for x in ("vulnerable", "critical", "super-effective", "especially effective")):
            reward += 5.0
    elif phase == 2:  # Navigation and hazards
        if command.lower() in {"n","s","e","w","ne","nw","se","sw","u","d","north","south","east","west","up","down"}:
            reward -= 1.0
        if new_room:
            reward += 50.0
        if any(x in text for x in ("trap", "trapped", "poisoned by", "hazard")):
            reward -= 500.0
        if any(x in text for x in ("patrol notices", "spotted by", "detects you")):
            reward -= 10.0
    else:  # Macro strategy / economy
        wealth_delta = after.wealth_copper - before.wealth_copper
        if wealth_delta > 0:
            reward += wealth_delta / 100.0  # +1 per gold equivalent earned
        if gear_name and command.lower().startswith("equip") and "you equip" in text:
            delta = gear_metric(after) - gear_metric(before)
            learner.gear[gear_name.lower()] = learner.gear.get(gear_name.lower(), 0.0) * 0.7 + delta * 0.3
            if delta > 0:
                reward += 100.0
            elif delta < 0:
                reward -= 25.0
        reserve = reserve_gold * 100
        if after.wealth_copper > reserve and command.lower().startswith("deposit") and "you deposit" in text:
            reward += 25.0
        if after.wealth_copper < reserve:
            # Borin must earn/find money, not withdraw it. There is no withdraw action in ACTIONS.
            if command in ("look", "score", "inventory", "services", "bank"):
                reward -= 5.0
        if command in ("look", "score", "inventory"):
            reward -= 1.0
    if any(x in text for x in ("unavailable:", "there is no generated", "cannot ", "could not ", "do not carry", "not found")):
        reward -= 2.0
    return reward, survival_bonus_due


def phase_schedule(config: dict[str, Any], total_hours: float) -> list[tuple[str, float]]:
    raw = [(str(x["name"]), float(x["hours"])) for x in config["curriculum"]]
    base = sum(h for _, h in raw)
    scale = total_hours / base if base else 1.0
    return [(name, hours * 3600.0 * scale) for name, hours in raw]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--hours", type=float, default=22.0)
    ap.add_argument("--player", default="Borin")
    ap.add_argument("--seed", type=int, default=None)
    args = ap.parse_args()
    if args.seed is not None:
        random.seed(args.seed)

    repo = Path(args.repo).resolve()
    root = repo / "AI" / "Training"
    runtime = root / "runtime"
    checkpoint = root / "checkpoints" / "borin_qlearning.json"
    config = read_json(root / "borin_training_config.json", {})
    if not config:
        raise RuntimeError("borin_training_config.json is missing or invalid")
    reserve_gold = int(config.get("economy", {}).get("carried_gold_reserve", 10))
    action_delay = float(config.get("action_delay_seconds", 0.15))
    checkpoint_seconds = float(config.get("checkpoint_seconds", 300))
    status_seconds = float(config.get("status_seconds", 30))

    runtime.mkdir(parents=True, exist_ok=True)
    pid_path = runtime / "trainer.pid"
    pid_path.write_text(str(os.getpid()), encoding="utf-8")
    log_path = runtime / "trainer.log"
    learner = QLearner(checkpoint)
    stop = False

    def on_stop(*_: Any) -> None:
        nonlocal stop
        stop = True
    signal.signal(signal.SIGINT, on_stop)
    signal.signal(signal.SIGTERM, on_stop)

    start = time.time()
    last_checkpoint = start
    last_status = 0.0
    last_survival_bonus = start
    visited_rooms: set[str] = set(learner.visits)
    schedule = phase_schedule(config, args.hours)
    total_seconds = sum(x[1] for x in schedule)
    client: HeadlessClient | None = None
    last_shop: list[tuple[str, int]] = []

    try:
        write_json_atomic(runtime / "status.json", {
            "phase": "Starting headless runtime", "elapsed": "0s", "remaining": f"{args.hours:.1f}h",
            "steps": learner.steps, "totalReward": learner.total_reward, "carriedGold": 0, "bankedGold": 0,
        })
        client = HeadlessClient(repo, args.player, log_path)
        state = client.state
        for phase_index, (phase_name, phase_seconds) in enumerate(schedule):
            phase_start = time.time()
            while not stop and time.time() - phase_start < phase_seconds:
                before = numeric_snapshot(state)
                key = state_key(phase_index, state, before)
                action_index = learner.choose(key, len(ACTIONS))
                action = ACTIONS[action_index]

                # Curriculum masks keep training focused while retaining cross-phase basics.
                if phase_index == 0 and action in {"buy_candidate", "sell_candidate", "deposit_surplus"}:
                    action = "explore"
                elif phase_index == 1 and action in {"buy_candidate", "sell_candidate", "deposit_surplus"}:
                    action = "attack"
                elif phase_index == 2 and action in {"buy_candidate", "sell_candidate", "deposit_surplus", "equip_candidate"}:
                    action = "explore"

                # Economy reserve is a hard policy boundary. At a bank, bank surplus before
                # taking discretionary actions; below reserve, never withdraw and do not buy.
                if phase_index == 3:
                    bank_text_now = clean(str(state.get("bank", ""))).lower()
                    if before.wealth_copper > reserve_gold * 100 and "carried:" in bank_text_now:
                        deposit_cmd = reserve_deposit_command(state, reserve_gold)
                        if deposit_cmd != "look":
                            action = "deposit_surplus"
                action_index = ACTIONS.index(action)
                command, gear_name = command_for(action, phase_index, state, learner, reserve_gold, last_shop)
                try:
                    next_state = client.send(command)
                except Exception:
                    # Preserve learning, then restart the authoritative runtime and continue.
                    learner.save()
                    if client:
                        client.close()
                    time.sleep(2.0)
                    client = HeadlessClient(repo, args.player, log_path)
                    state = client.state
                    continue

                if command == "list":
                    parsed = shop_items(str(next_state.get("response", "")))
                    if parsed:
                        last_shop = parsed

                after = numeric_snapshot(next_state)
                room_id = after.room
                new_room = bool(room_id and room_id not in visited_rooms)
                if room_id:
                    visited_rooms.add(room_id)
                    learner.visits[room_id] = learner.visits.get(room_id, 0) + 1

                reward, paid_survival = reward_for(
                    phase_index, before, after, command, str(next_state.get("response", "")),
                    new_room, gear_name, learner, reserve_gold, time.time() - last_survival_bonus,
                )
                if paid_survival:
                    last_survival_bonus = time.time()
                next_key = state_key(phase_index, next_state, after)
                learner.update(key, action_index, reward, next_key, len(ACTIONS))
                state = next_state

                # Death does not erase learned policy; reset only the session wrapper.
                if after.dead:
                    state = client.reset_session()

                now = time.time()
                if now - last_checkpoint >= checkpoint_seconds:
                    learner.save()
                    last_checkpoint = now
                if now - last_status >= status_seconds:
                    elapsed = now - start
                    remaining = max(0.0, total_seconds - elapsed)
                    bank_text = clean(str(state.get("bank", "")))
                    denoms = parse_carried_denominations(bank_text)
                    carried_copper = sum(denoms[k] * v for k, v in {"runic":1000000,"platinum":10000,"gold":100,"silver":10,"copper":1}.items())
                    if carried_copper == 0:
                        carried_copper = numeric_snapshot(state).wealth_copper
                    banked_gold = "unknown"
                    m = re.search(r"Banked:\s*(.*)", bank_text, re.I)
                    if m:
                        banked_gold = m.group(1).strip()
                    write_json_atomic(runtime / "status.json", {
                        "phase": phase_name,
                        "elapsed": f"{elapsed/3600:.2f}h",
                        "remaining": f"{remaining/3600:.2f}h",
                        "steps": learner.steps,
                        "totalReward": round(learner.total_reward, 2),
                        "epsilon": round(learner.epsilon, 4),
                        "room": after.room,
                        "carriedGold": round(carried_copper / 100.0, 2),
                        "bankedGold": banked_gold,
                        "reserveRule": f"Keep {reserve_gold} gold carried; bank surplus; never withdraw to refill reserve.",
                        "lastCommand": command,
                        "lastResponse": clean(str(state.get("response", "")))[:500],
                    })
                    last_status = now
                if action_delay > 0:
                    time.sleep(action_delay)
            if stop:
                break

        learner.save()
        elapsed = time.time() - start
        write_json_atomic(runtime / "status.json", {
            "phase": "Stopped" if stop else "Complete",
            "elapsed": f"{elapsed/3600:.2f}h",
            "remaining": "0h" if not stop else "stopped early",
            "steps": learner.steps,
            "totalReward": round(learner.total_reward, 2),
            "carriedGold": round(numeric_snapshot(state).wealth_copper / 100.0, 2),
            "bankedGold": "see last bank snapshot",
        })
        return 0
    except Exception as exc:
        write_json_atomic(runtime / "status.json", {
            "phase": "Faulted", "elapsed": f"{(time.time()-start)/3600:.2f}h", "remaining": "unknown",
            "steps": learner.steps, "totalReward": round(learner.total_reward, 2),
            "carriedGold": "unknown", "bankedGold": "unknown", "error": str(exc),
        })
        with log_path.open("a", encoding="utf-8") as log:
            log.write(f"TRAINER ERROR: {exc}\n")
        return 1
    finally:
        try:
            learner.save()
        except Exception:
            pass
        if client:
            client.close()
        try:
            pid_path.unlink(missing_ok=True)
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
