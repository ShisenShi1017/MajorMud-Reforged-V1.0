#!/usr/bin/env python3
"""Borin continuous headless reinforcement-learning trainer and runtime tester.

The trainer talks only to Reforged.Server's newline-delimited JSON stdin/stdout
headless protocol. It uses dependency-free tabular Q-learning, continuously
rotates through major gameplay systems, preserves learned state between runs,
and writes an aggregated defect report while it plays.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import re
import signal
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

ANSI = re.compile(r"\x1b\[[0-9;]*m")
DIRECTIONS = {
    "n", "s", "e", "w", "ne", "nw", "se", "sw", "u", "d",
    "north", "south", "east", "west", "northeast", "northwest",
    "southeast", "southwest", "up", "down",
}
CURRENCY_VALUES = {"runic": 1_000_000, "platinum": 10_000, "gold": 100, "silver": 10, "copper": 1}

PHASE_ACTIONS: list[list[str]] = [
    ["look", "score", "health", "rest", "meditate", "recover", "inventory", "flee", "explore"],
    ["look", "exits", "search", "scan", "hide", "sneak", "explore", "open_exit", "close_exit", "picklock_exit", "disarm_exit", "get_all"],
    ["combat", "attack", "kill", "bash", "smash", "backstab", "punch", "kick", "jumpkick", "flee", "score", "explore"],
    ["spells", "powers", "abilities", "skills", "cast_candidate", "invoke_candidate", "use_candidate", "read_candidate", "dispel", "score", "explore"],
    ["quest", "journal", "greet_npc", "ask_npc_quest", "ask_npc_help", "services", "look", "explore"],
    ["inventory", "equipment", "keys", "get_all", "appraise_candidate", "equip_candidate", "wear_candidate", "wield_candidate", "repair_candidate", "use_candidate", "explore"],
    ["experience", "character", "profile", "train", "tat", "ready_candidate", "score", "skills", "abilities", "explore"],
    ["wealth", "services", "list", "bank", "deposit_surplus", "buy_candidate", "sell_candidate", "equip_candidate", "repair_candidate", "get_all", "attack", "explore"],
]

# CodeUpdate0369 parity-complete regression contract. Borin still learns through the
# same player runtime, but he now refuses to silently train around a Classic parity
# regression. The cumulative gate already includes the final-strong gate; both are
# run so the tester report can localize a failure immediately.
PARITY_SCORECARD_EXPECTED = {
    "PassCount": 200,
    "StrongCount": 0,
    "UnknownCount": 0,
    "ScoredRequirementCount": 200,
    "StrictPercent": 100.0,
}
DEFAULT_PARITY_GATES = [
    "--smoke-classic-final-strong-closure",
    "--smoke-classic-parity-closure",
]
PLAYER_VISIBLE_PARITY_PROBES = [
    "help topics",
    "help top",
    "topten",
    "top 10",
    "top 10 gangs",
]


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


def utc_now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def _extract_json_object(text: str) -> dict[str, Any] | None:
    decoder = json.JSONDecoder()
    for match in re.finditer(r"\{", text or ""):
        try:
            value, _ = decoder.raw_decode((text or "")[match.start():])
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict) and ("Passed" in value or "passed" in value):
            return value
    return None


def validate_parity_scorecard(repo: Path) -> tuple[bool, str]:
    scorecard_path = repo / "Reports" / "RuntimeVerification" / "majormud_parity_scorecard.json"
    scorecard = read_json(scorecard_path, None)
    if not isinstance(scorecard, dict):
        return False, f"Parity scorecard is missing or invalid: {scorecard_path}"
    mismatches = [
        f"{key}: expected {expected!r}, got {scorecard.get(key)!r}"
        for key, expected in PARITY_SCORECARD_EXPECTED.items()
        if scorecard.get(key) != expected
    ]
    if mismatches:
        return False, "; ".join(mismatches)
    return True, "200 PASS / 0 STRONG / 0 UNKNOWN; strict parity 100%."


def run_parity_smoke(repo: Path, gate: str, timeout_seconds: float, log_path: Path) -> dict[str, Any]:
    project = repo / "Tools" / "ReforgedValidator" / "DotNet" / "Reforged.Validation.csproj"
    cmd = ["dotnet", "run", "--project", str(project), "--", str(repo), gate]
    started = time.time()
    try:
        completed = subprocess.run(
            cmd,
            cwd=str(repo),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=max(30.0, timeout_seconds),
            check=False,
        )
        combined = (completed.stdout or "") + "\n" + (completed.stderr or "")
        parsed = _extract_json_object(completed.stdout or "")
        passed_value = None if parsed is None else parsed.get("Passed", parsed.get("passed"))
        passed = completed.returncode == 0 and passed_value is True
        failures = []
        if isinstance(parsed, dict):
            raw_failures = parsed.get("Failures", parsed.get("failures", []))
            if isinstance(raw_failures, list):
                failures = [str(x) for x in raw_failures]
            if not failures:
                raw_checks = parsed.get("Checks", parsed.get("checks", []))
                if isinstance(raw_checks, list):
                    failures = [str(x) for x in raw_checks if str(x).lower().endswith(":false")]
        detail = "; ".join(failures) if failures else clean(combined)[-1200:].strip()
        with log_path.open("a", encoding="utf-8") as log:
            log.write(f"PARITY GATE {gate}: exit={completed.returncode}; passed={passed}; elapsed={time.time()-started:.2f}s\n")
            if not passed and combined.strip():
                log.write(clean(combined)[-4000:] + "\n")
        return {
            "gate": gate,
            "passed": passed,
            "exitCode": completed.returncode,
            "elapsedSeconds": round(time.time() - started, 2),
            "failures": failures,
            "detail": detail[:1200],
            "checkedUtc": utc_now(),
        }
    except Exception as exc:
        with log_path.open("a", encoding="utf-8") as log:
            log.write(f"PARITY GATE {gate} ERROR: {exc}\n")
        return {
            "gate": gate,
            "passed": False,
            "exitCode": None,
            "elapsedSeconds": round(time.time() - started, 2),
            "failures": [type(exc).__name__],
            "detail": str(exc)[:1200],
            "checkedUtc": utc_now(),
        }


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
    level: int = 0
    room: str = ""
    dead: bool = False


def numeric_snapshot(state: dict[str, Any]) -> Snapshot:
    score = clean(str(state.get("score", "")))
    wealth = clean(str(state.get("wealth", "")))
    response = clean(str(state.get("response", ""))).lower()
    hp = hp_max = mana = mana_max = exp = armor_a = armor_b = 0
    m = re.search(r"Hits:\s*(-?\d+)\s*/\s*(\d+)", score, re.I)
    if m:
        hp, hp_max = map(int, m.groups())
    m = re.search(r"(?:Mana|Kai):\s*\*?\s*(-?\d+)\s*/\s*(\d+)", score, re.I)
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
    actor = state.get("actor") or {}
    level = int(actor.get("level") or 0)
    room = str((state.get("room") or {}).get("id", ""))
    dead = (hp_max > 0 and hp <= 0) or any(x in response for x in ("you have died", "you are dead", "you die"))
    return Snapshot(hp, hp_max, mana, mana_max, copper, exp, armor_a, armor_b, level, room, dead)


def gear_metric(s: Snapshot) -> float:
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
    out: dict[str, int] = {}
    for name, pat in patterns.items():
        m = re.search(pat, carried, re.I)
        out[name] = int(m.group(1)) if m else 0
    return out


def carried_copper_from_state(state: dict[str, Any]) -> int:
    denoms = parse_carried_denominations(str(state.get("bank", "")))
    total = sum(denoms[k] * CURRENCY_VALUES[k] for k in CURRENCY_VALUES)
    return total or numeric_snapshot(state).wealth_copper


def inventory_items(text: str) -> list[str]:
    text = clean(text)
    m = re.search(r"You are carrying (.+?)(?:\n|$)", text, re.I)
    if not m or "nothing" in m.group(1).lower():
        return []
    parts = [x.strip().rstrip(".") for x in m.group(1).split(",")]
    result: list[str] = []
    for part in parts:
        low = part.lower()
        if any(word in low for word in ("runic", "platinum", "gold crown", "silver noble", "copper farthing")):
            continue
        if "(" in part and ")" in part:
            continue
        part = re.sub(r"^\d+\s+", "", part).strip()
        if part:
            result.append(part)
    return result


def shop_items(text: str) -> list[tuple[str, int]]:
    rows: list[tuple[str, int]] = []
    for line in clean(text).splitlines():
        m = re.match(r"^(.{1,40}?)\s{2,}(\d+)\s+(Free|\d+\s+gold crowns?)\s*$", line, re.I)
        if not m:
            continue
        name = m.group(1).strip()
        price = 0 if m.group(3).lower() == "free" else int(re.search(r"\d+", m.group(3)).group(0))
        if int(m.group(2)) > 0:
            rows.append((name, price))
    return rows


def listing_candidates(text: str, maximum: int = 20) -> list[str]:
    result: list[str] = []
    for raw in clean(text).splitlines():
        line = raw.strip()
        if not line or len(line) > 100:
            continue
        low = line.lower()
        if any(low.startswith(prefix) for prefix in (
            "known ", "available ", "spells", "powers", "abilities", "skills", "you know", "you have no ", "you have the following ", "level ", "none", "no ", "usage:", "---"
        )):
            continue
        line = re.sub(r"^[*+\-•>\d.()\[\]\s]+", "", line).strip()
        if not line:
            continue
        # Prefer the first textual column and strip obvious resource/status suffixes.
        name = re.split(r"\s{2,}|\t|\s+\(cost|\s+cost[:=]|\s+level\s+\d", line, maxsplit=1, flags=re.I)[0].strip(" .:-")
        if 1 < len(name) <= 60 and any(ch.isalpha() for ch in name):
            if name.lower() not in {x.lower() for x in result}:
                result.append(name)
        if len(result) >= maximum:
            break
    return result


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
        choices = [i for i, value in enumerate(values) if value == best]
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
            "updatedUtc": utc_now(),
        })


class TrainingTester:
    SEVERITY_RANK = {"critical": 4, "high": 3, "medium": 2, "low": 1, "info": 0}
    # Historical parity-gap records for commands promoted by the player runtime contract
    # are no longer active defects. Keep lifetime counts, but retire them from the current
    # defect queue so Borin does not relearn or repeatedly report behavior that was fixed.
    RESOLVED_PARITY_COMMANDS = {"health", "invoke", "exits", "ready", "profile", "scan"}

    def __init__(self, json_path: Path, markdown_path: Path, config: dict[str, Any]):
        self.json_path = json_path
        self.markdown_path = markdown_path
        self.enabled = bool(config.get("enabled", True))
        self.excerpt_chars = max(100, int(config.get("response_excerpt_chars", 600)))
        self.maximum_unique = max(100, int(config.get("maximum_unique_defects", 2000)))
        existing = read_json(json_path, {})
        defects = existing.get("defects", []) if isinstance(existing, dict) else []
        summary = existing.get("summary", {}) if isinstance(existing, dict) and isinstance(existing.get("summary"), dict) else {}
        legacy_total = int(summary.get("totalOccurrences", 0))
        self.lifetime_occurrences = int(summary.get("lifetimeOccurrences", legacy_total))
        self.retired_occurrences = int(summary.get("retiredOccurrences", 0))
        self.defects: dict[str, dict[str, Any]] = {}
        retired_now = 0
        for defect in defects:
            if not isinstance(defect, dict) or not defect.get("id"):
                continue
            command_name = str(defect.get("command", "")).split(" ", 1)[0].lower()
            is_resolved_parity_gap = (
                str(defect.get("category", "")) == "parity-gap"
                and command_name in self.RESOLVED_PARITY_COMMANDS
            )
            if is_resolved_parity_gap:
                retired_now += int(defect.get("occurrences", 0))
                continue
            self.defects[str(defect["id"])] = defect
        self.retired_occurrences += retired_now
        self.total_occurrences = sum(int(item.get("occurrences", 0)) for item in self.defects.values())
        self.dirty = retired_now > 0

    def _fingerprint(self, category: str, command: str, room: str, detail: str) -> str:
        stable_detail = re.sub(r"\b\d{4,}\b", "#", clean(detail).lower())[:300]
        raw = f"{category}|{command.lower()}|{room.lower()}|{stable_detail}"
        return hashlib.sha1(raw.encode("utf-8", errors="replace")).hexdigest()[:12]

    def record(self, severity: str, category: str, title: str, command: str, room: str,
               phase: str, cycle: int, detail: str) -> None:
        if not self.enabled:
            return
        detail = clean(detail).strip()[:self.excerpt_chars]
        defect_id = self._fingerprint(category, command, room, detail or title)
        now = utc_now()
        item = self.defects.get(defect_id)
        if item is None:
            if len(self.defects) >= self.maximum_unique:
                # Keep the most useful long-run data bounded.
                oldest = min(self.defects.values(), key=lambda x: str(x.get("lastSeenUtc", "")), default=None)
                if oldest is not None:
                    self.defects.pop(str(oldest.get("id", "")), None)
            item = {
                "id": defect_id,
                "severity": severity,
                "category": category,
                "title": title,
                "command": command,
                "room": room,
                "phase": phase,
                "cycle": cycle,
                "occurrences": 0,
                "firstSeenUtc": now,
                "lastSeenUtc": now,
                "detail": detail,
            }
            self.defects[defect_id] = item
        item["occurrences"] = int(item.get("occurrences", 0)) + 1
        item["lastSeenUtc"] = now
        item["phase"] = phase
        item["cycle"] = cycle
        if detail:
            item["detail"] = detail
        if self.SEVERITY_RANK.get(severity, 0) > self.SEVERITY_RANK.get(str(item.get("severity", "info")), 0):
            item["severity"] = severity
        self.total_occurrences += 1
        self.lifetime_occurrences += 1
        self.dirty = True

    def observe(self, before: Snapshot, after: Snapshot, state: dict[str, Any], command: str,
                phase: str, cycle: int) -> None:
        if not self.enabled:
            return
        room = after.room or before.room or "unknown"
        response = clean(str(state.get("response", ""))).strip()
        error = clean(str(state.get("error") or "")).strip()
        low = response.lower()

        if error:
            self.record("critical", "protocol-error", "Headless runtime returned a protocol error", command, room, phase, cycle, error)

        critical_markers = (
            "unhandled exception", "nullreferenceexception", "keynotfoundexception", "indexoutofrangeexception",
            "invalidoperationexception", "object reference not set", "stack trace", "fatal error",
        )
        if any(marker in low for marker in critical_markers):
            self.record("critical", "runtime-exception", "Gameplay command exposed a runtime exception", command, room, phase, cycle, response)

        high_markers = (
            "live gameplay facade is not attached", "has no executable dispatcher branch", "not implemented",
            "not supported by this runtime", "server bootstrap failed",
        )
        if any(marker in low for marker in high_markers):
            self.record("high", "runtime-route", "Registered gameplay path is not executable", command, room, phase, cycle, response)

        if "recognized, but its exact runtime behavior is not yet recovered" in low:
            self.record("medium", "parity-gap", "Recognized command still lacks recovered runtime behavior", command, room, phase, cycle, response)

        for field in ("score", "health", "exits", "profile", "scan", "character", "experience", "wealth", "inventory", "equipment", "keys", "combat", "spells", "powers", "abilities", "skills", "quest", "journal", "services", "bank"):
            probe = clean(str(state.get(field, ""))).strip()
            if probe.lower().startswith("unavailable:"):
                self.record("high", "headless-state-probe", f"Headless state probe '{field}' failed", command, room, phase, cycle, probe)

        # Borin is a normal player. Keep his training assumptions synchronized with the
        # server's player-visible compatibility contract instead of teaching around parity gaps.
        runtime = state.get("runtime") if isinstance(state.get("runtime"), dict) else {}
        contract = runtime.get("playerContract") if isinstance(runtime.get("playerContract"), dict) else {}
        expected_contract = {
            "version": "majorMud-player-2026-pass09",
            "terminalEncoding": "CP437",
            "privateClientDataOutOfBand": True,
            "megaMudDirectLoginSupported": True,
            "megaMudCharacterSelectorSupported": True,
            "megaMudUsesTelnetPortOnly": True,
            "defaultPromptAnsiFree": True,
            "healthCommandSupported": True,
            "exitsCommandSupported": True,
            "readyCommandSupported": True,
            "profileCommandSupported": True,
            "scanExactBehaviorRecovered": True,
            "scanDisplaysCurrentAdventurers": True,
            "warningBlocksProvenPositiveEvilPointActions": True,
            "warningOffAllowsProvenEvilPointActions": True,
            "rerollCharacterReplacementConnected": True,
            "rerollDiscardsOldCharacterState": True,
            "keepControlsRerollExperienceRetention": True,
            "classicCombatRoundMilliseconds": 5000,
            "castRejectsKaiClasses": True,
            "invokeRequiresKaiClass": True,
            "decodedRoomActionsUseNormalPlayerCommands": True,
        }
        for key, expected in expected_contract.items():
            if contract.get(key) != expected:
                self.record("high", "player-runtime-contract", f"Borin player runtime contract mismatch: {key}", command, room, phase, cycle,
                            f"expected={expected!r}; actual={contract.get(key)!r}")

        health_probe = clean(str(state.get("health", ""))).strip()
        if "recognized, but its exact runtime behavior is not yet recovered" in health_probe.lower():
            self.record("medium", "player-runtime-contract", "HEALTH regressed to an evidence-gated placeholder", command, room, phase, cycle, health_probe)
        elif health_probe and not re.search(r"^Health:\s*-?\d+/\d+\s+(?:Mana|Kai):\s*-?\d+/\d+$", health_probe, re.I):
            self.record("medium", "player-runtime-contract", "HEALTH response no longer matches Borin's player-state contract", command, room, phase, cycle, health_probe)

        exits_probe = clean(str(state.get("exits", ""))).strip()
        if "recognized, but its exact runtime behavior is not yet recovered" in exits_probe.lower():
            self.record("medium", "player-runtime-contract", "EXITS regressed to an evidence-gated placeholder", command, room, phase, cycle, exits_probe)
        elif exits_probe and not exits_probe.lower().startswith("obvious exits:"):
            self.record("medium", "player-runtime-contract", "EXITS response no longer matches the room-presentation contract", command, room, phase, cycle, exits_probe)

        scan_probe = clean(str(state.get("scan", ""))).strip()
        if "recognized, but its exact runtime behavior is not yet recovered" in scan_probe.lower():
            self.record("medium", "player-runtime-contract", "SCAN regressed to an evidence-gated placeholder", command, room, phase, cycle, scan_probe)
        elif scan_probe and not (scan_probe.lower().startswith("current adventurers") or scan_probe.lower().startswith("there are no users in the game")):
            self.record("medium", "player-runtime-contract", "SCAN response no longer matches the current-adventurer scan contract", command, room, phase, cycle, scan_probe)

        profile_probe = clean(str(state.get("profile", ""))).strip()
        if "recognized, but its exact runtime behavior is not yet recovered" in profile_probe.lower():
            self.record("medium", "player-runtime-contract", "PROFILE regressed to an evidence-gated placeholder", command, room, phase, cycle, profile_probe)
        elif profile_probe and not profile_probe.lower().startswith("current profile settings:"):
            self.record("medium", "player-runtime-contract", "PROFILE response no longer exposes current player settings", command, room, phase, cycle, profile_probe)

        raw_prompt = str(state.get("prompt", ""))
        plain_prompt = clean(raw_prompt).strip()
        if plain_prompt.startswith("[HP=") and "\x1b[" in raw_prompt:
            self.record("high", "player-runtime-contract", "Default HP prompt contains ANSI and may desynchronize legacy clients", command, room, phase, cycle, raw_prompt)

        # State-query commands should always render something useful. Empty output here is suspicious,
        # while empty output from movement/dialogue actions can be legitimate and is not flagged.
        if not response and command.split(" ", 1)[0].lower() in {
            "score", "health", "exits", "profile", "scan", "inventory", "equipment", "wealth", "experience", "character",
            "spells", "powers", "abilities", "skills", "quest", "journal", "services", "bank",
        }:
            self.record("medium", "empty-state-response", "State query returned an empty response", command, room, phase, cycle, "empty response")

        if after.hp_max > 0 and (after.hp < 0 or after.hp > after.hp_max):
            self.record("high", "state-invariant", "Hit points are outside the valid range", command, room, phase, cycle, f"HP={after.hp}/{after.hp_max}")
        if after.mana_max > 0 and (after.mana < 0 or after.mana > after.mana_max):
            self.record("high", "state-invariant", "Mana/Kai is outside the valid range", command, room, phase, cycle, f"Mana={after.mana}/{after.mana_max}")
        if not room:
            self.record("critical", "state-invariant", "Headless state lost the current room identity", command, "unknown", phase, cycle, response)

        # Exit data that the authoritative runtime already marks invalid is a useful tester finding.
        for exit_info in (state.get("room") or {}).get("exitDetails", []) or []:
            if not isinstance(exit_info, dict) or not exit_info.get("hasBlockedDestination"):
                continue
            direction = str(exit_info.get("direction") or "?")
            target = str(exit_info.get("targetRoomId") or exit_info.get("targetRoomNumber") or "unknown")
            detail = f"direction={direction}; target={target}; destinationStatus={exit_info.get('destinationStatus','')}; validationStatus={exit_info.get('validationStatus','')}; runtimeDisposition={exit_info.get('runtimeDisposition','')}"
            self.record("medium", "blocked-exit-data", "Room exit points to a blocked or unresolved destination", f"move {direction}", room, phase, cycle, detail)

        cmd_low = command.lower()
        if cmd_low.startswith("deposit ") and "you deposit" in low and after.wealth_copper > before.wealth_copper:
            self.record("high", "economy-invariant", "Successful deposit increased carried wealth", command, room, phase, cycle, f"before={before.wealth_copper}; after={after.wealth_copper}; {response}")
        if cmd_low.startswith("buy ") and ("you buy" in low or "you purchase" in low) and after.wealth_copper > before.wealth_copper:
            self.record("high", "economy-invariant", "Successful purchase increased carried wealth", command, room, phase, cycle, f"before={before.wealth_copper}; after={after.wealth_copper}; {response}")
        if cmd_low.startswith("sell ") and "you sell" in low and after.wealth_copper < before.wealth_copper:
            self.record("high", "economy-invariant", "Successful sale reduced carried wealth", command, room, phase, cycle, f"before={before.wealth_copper}; after={after.wealth_copper}; {response}")

    def observe_parity_gate(self, result: dict[str, Any], cycle: int) -> None:
        if bool(result.get("passed")):
            return
        gate = str(result.get("gate") or "parity-gate")
        detail = str(result.get("detail") or result.get("failures") or "Parity regression gate failed")
        self.record(
            "critical",
            "classic-parity-regression",
            "Authoritative Classic parity smoke gate failed",
            gate,
            "preflight",
            "100% Classic parity regression",
            cycle,
            detail,
        )

    def observe_player_parity_probe(self, command: str, state: dict[str, Any], cycle: int) -> bool:
        response = clean(str(state.get("response", ""))).strip()
        low = response.lower()
        room = str((state.get("room") or {}).get("id") or "unknown")
        if state.get("error"):
            self.record("high", "parity-player-probe", "Player-visible parity probe returned an error", command, room, "100% Classic parity regression", cycle, str(state.get("error")))
            return False
        if not response:
            self.record("high", "parity-player-probe", "Player-visible parity probe returned an empty response", command, room, "100% Classic parity regression", cycle, "empty response")
            return False
        if any(marker in low for marker in (
            "recognized, but its exact runtime behavior is not yet recovered",
            "not implemented",
            "not supported by this runtime",
            "has no executable dispatcher branch",
        )):
            self.record("high", "parity-player-probe", "Player-visible parity probe regressed to a placeholder", command, room, "100% Classic parity regression", cycle, response)
            return False
        command_low = command.lower()
        if command_low.startswith("help ") and "the following help topics are currently available:" not in low:
            self.record("medium", "parity-player-probe", "HELP TOPICS/prefix no longer renders the recovered native topic surface", command, room, "100% Classic parity regression", cycle, response)
            return False
        if command_low.startswith("top") and not any(marker in low for marker in ("experience=", "no topten-eligible", "top count")):
            self.record("medium", "parity-player-probe", "TOP/TOPTEN response no longer matches the recovered ranking surface", command, room, "100% Classic parity regression", cycle, response)
            return False
        return True

    def record_restart(self, command: str, room: str, phase: str, cycle: int, error: Exception) -> None:
        self.record("critical", "server-restart", "Headless server failed and had to be restarted", command, room or "unknown", phase, cycle, str(error))

    def summary(self) -> dict[str, Any]:
        values = list(self.defects.values())
        critical = sum(1 for x in values if x.get("severity") == "critical")
        high = sum(1 for x in values if x.get("severity") == "high")
        return {
            "uniqueDefects": len(values),
            "totalOccurrences": self.total_occurrences,
            "lifetimeOccurrences": self.lifetime_occurrences,
            "retiredOccurrences": self.retired_occurrences,
            "critical": critical,
            "high": high,
        }

    def last_defect(self) -> dict[str, Any] | None:
        if not self.defects:
            return None
        return max(self.defects.values(), key=lambda x: str(x.get("lastSeenUtc", "")))

    def save(self, force: bool = False) -> None:
        if not self.enabled or (not force and not self.dirty):
            return
        ordered = sorted(
            self.defects.values(),
            key=lambda x: (
                self.SEVERITY_RANK.get(str(x.get("severity", "info")), 0),
                int(x.get("occurrences", 0)),
                str(x.get("lastSeenUtc", "")),
            ),
            reverse=True,
        )
        payload = {
            "schema": "borin-training-defects-v2",
            "updatedUtc": utc_now(),
            "summary": self.summary(),
            "defects": ordered,
        }
        write_json_atomic(self.json_path, payload)
        lines = [
            "# Borin Continuous Training Tester Report",
            "",
            f"Updated: {payload['updatedUtc']}",
            f"Active defects: {payload['summary']['uniqueDefects']}; active occurrences: {payload['summary']['totalOccurrences']}; lifetime occurrences: {payload['summary']['lifetimeOccurrences']}; retired resolved occurrences: {payload['summary']['retiredOccurrences']}; critical: {payload['summary']['critical']}; high: {payload['summary']['high']}",
            "",
        ]
        if not ordered:
            lines.append("No defects have been recorded yet.")
        else:
            for item in ordered:
                lines.extend([
                    f"## [{str(item.get('severity','info')).upper()}] {item.get('title','Defect')} ({item.get('id','')})",
                    f"- Occurrences: {item.get('occurrences', 0)}",
                    f"- Command: `{item.get('command','')}`",
                    f"- Room: `{item.get('room','')}`",
                    f"- Phase/cycle: {item.get('phase','')} / {item.get('cycle', 0)}",
                    f"- First seen: {item.get('firstSeenUtc','')}",
                    f"- Last seen: {item.get('lastSeenUtc','')}",
                    f"- Detail: {item.get('detail','')}",
                    "",
                ])
        self.markdown_path.write_text("\n".join(lines), encoding="utf-8")
        self.dirty = False


def state_key(phase: int, state: dict[str, Any], snap: Snapshot) -> str:
    room = state.get("room") or {}
    hp_bucket = 0 if snap.hp_max <= 0 else min(4, int((snap.hp / max(1, snap.hp_max)) * 4))
    wealth_bucket = min(9, snap.wealth_copper // 1000)
    npc = 1 if room.get("visibleNpcs") else 0
    services = 0 if "no generated services" in clean(str(state.get("services", ""))).lower() else 1
    combat = 0 if "not in combat" in clean(str(state.get("combat", ""))).lower() else 1
    return f"{phase}|{room.get('id','')}|h{hp_bucket}|w{wealth_bucket}|n{npc}|s{services}|c{combat}|l{snap.level}"


def choose_exit(state: dict[str, Any]) -> str:
    room = state.get("room") or {}
    exits = [str(x) for x in room.get("exits", []) if str(x).strip()]
    return random.choice(exits) if exits else "look"


def first_npc_name(state: dict[str, Any]) -> str | None:
    npcs = (state.get("room") or {}).get("visibleNpcs") or []
    if not npcs:
        return None
    npc = random.choice(npcs)
    return str(npc.get("displayName") or npc.get("DisplayName") or "").strip() or None


def reserve_deposit_command(state: dict[str, Any], reserve_gold: int) -> str:
    bank_text = clean(str(state.get("bank", "")))
    if "unavailable:" in bank_text.lower() or "no generated bank" in bank_text.lower():
        return "look"
    denoms = parse_carried_denominations(bank_text)
    total = sum(denoms[k] * CURRENCY_VALUES[k] for k in CURRENCY_VALUES)
    reserve = reserve_gold * 100
    surplus = max(0, total - reserve)
    if surplus <= 0:
        return "look"
    for name in ("runic", "platinum", "gold", "silver", "copper"):
        unit = CURRENCY_VALUES[name]
        amount = min(denoms[name], surplus // unit)
        if amount > 0:
            return f"deposit {amount} {name}"
    return "look"


def command_for(action: str, phase: int, state: dict[str, Any], learner: QLearner,
                reserve_gold: int, last_shop: list[tuple[str, int]]) -> tuple[str, str | None]:
    snap = numeric_snapshot(state)
    inventory = inventory_items(str(state.get("inventory", "")))
    npc = first_npc_name(state)

    if action == "explore":
        return choose_exit(state), None
    if action == "get_all":
        return "get all", None
    if action in {"attack", "kill", "bash", "smash", "backstab", "punch", "kick", "jumpkick"}:
        return (f"{action} {npc}", None) if npc else ("look", None)
    if action == "greet_npc":
        return (f"greet {npc}", None) if npc else ("look", None)
    if action == "ask_npc_quest":
        return (f"ask {npc} quest", None) if npc else ("look", None)
    if action == "ask_npc_help":
        return (f"ask {npc} help", None) if npc else ("look", None)
    if action in {"open_exit", "close_exit", "picklock_exit", "disarm_exit"}:
        direction = choose_exit(state)
        if direction == "look":
            return "look", None
        if action == "open_exit":
            return f"open {direction}", None
        if action == "close_exit":
            return f"close {direction}", None
        if action == "picklock_exit":
            return f"picklock {direction}", None
        return f"disarm trap {direction}", None
    if action == "deposit_surplus":
        return reserve_deposit_command(state, reserve_gold), None
    if action == "buy_candidate":
        if snap.wealth_copper < reserve_gold * 100:
            return "look", None
        affordable = [(name, price) for name, price in last_shop if snap.wealth_copper - price * 100 >= reserve_gold * 100]
        if not affordable:
            return "list", None
        if random.random() < learner.epsilon:
            name, _ = random.choice(affordable)
        else:
            name, _ = max(affordable, key=lambda x: learner.gear.get(x[0].lower(), 0.0) - x[1] * 0.1)
        return f"buy {name}", name
    if action == "sell_candidate":
        if not inventory:
            return "look", None
        name = min(inventory, key=lambda x: learner.gear.get(x.lower(), 0.0))
        return f"sell {name}", name
    if action in {"equip_candidate", "wear_candidate", "wield_candidate", "ready_candidate"}:
        if not inventory:
            return "look", None
        if random.random() < learner.epsilon:
            name = random.choice(inventory)
        else:
            name = max(inventory, key=lambda x: learner.gear.get(x.lower(), 0.0))
        verb = action.split("_", 1)[0]
        return f"{verb} {name}", name
    if action in {"repair_candidate", "appraise_candidate", "use_candidate", "read_candidate"}:
        if not inventory:
            return "look", None
        name = random.choice(inventory)
        verb = action.split("_", 1)[0]
        return f"{verb} {name}", name
    if action == "cast_candidate":
        candidates = listing_candidates(str(state.get("spells", "")))
        if not candidates:
            return "spells", None
        name = random.choice(candidates)
        return (f"cast {name} {npc}", name) if npc else (f"cast {name}", name)
    if action == "invoke_candidate":
        candidates = listing_candidates(str(state.get("powers", "")))
        if not candidates:
            return "powers", None
        name = random.choice(candidates)
        return (f"invoke {name} {npc}", name) if npc else (f"invoke {name}", name)
    if action == "dispel":
        return "dispel self", None
    return action, None


def reward_for(phase: int, before: Snapshot, after: Snapshot, command: str, response: str,
               new_room: bool, gear_name: str | None, learner: QLearner, reserve_gold: int,
               elapsed_since_survival_bonus: float) -> tuple[float, bool]:
    text = clean(response).lower()
    reward = 0.0
    survival_bonus_due = elapsed_since_survival_bonus >= 30.0
    if after.dead:
        reward -= 1000.0
    reward -= float(max(0, before.hp - after.hp))

    if phase == 0:  # Survival & Recovery
        if survival_bonus_due and not after.dead:
            reward += 10.0
        if command.startswith("flee") and before.hp_max and before.hp / max(1, before.hp_max) < 0.25 and "cannot" not in text:
            reward += 5.0
        if after.hp > before.hp:
            reward += min(25.0, float(after.hp - before.hp))
    elif phase == 1:  # Exploration & Hazards
        if command.lower() in DIRECTIONS:
            reward -= 1.0
        if new_room:
            reward += 50.0
        if any(x in text for x in ("trap", "trapped", "poisoned by", "hazard")):
            reward -= 500.0
    elif phase == 2:  # Combat
        if any(x in text for x in ("you kill", "you have slain", "you slay", "is dead", "defeated")):
            reward += 100.0
        reward -= max(0, before.mana - after.mana)
        if any(x in text for x in ("vulnerable", "critical", "especially effective")):
            reward += 5.0
    elif phase == 3:  # Spells/Powers/Item Use
        if command.split(" ", 1)[0] in {"cast", "invoke", "use", "read", "dispel"} and not any(x in text for x in ("cannot", "not found", "do not", "unavailable:")):
            reward += 10.0
        if after.mana < before.mana and any(x in text for x in ("no effect", "cannot", "fails", "failed")):
            reward -= 10.0
    elif phase == 4:  # NPCs/Dialogue/Quests
        if after.exp > before.exp:
            reward += min(500.0, float(after.exp - before.exp))
        if any(x in text for x in ("quest accepted", "you accept", "quest complete", "completed quest", "reward")):
            reward += 50.0
        if command.startswith(("greet ", "ask ")) and len(text) > 20 and "does not respond" not in text:
            reward += 5.0
    elif phase == 5:  # Inventory/Equipment
        if gear_name and command.lower().startswith(("equip ", "wear ", "wield ")) and any(x in text for x in ("you equip", "you wear", "you wield")):
            delta = gear_metric(after) - gear_metric(before)
            learner.gear[gear_name.lower()] = learner.gear.get(gear_name.lower(), 0.0) * 0.7 + delta * 0.3
            reward += 100.0 if delta > 0 else (-25.0 if delta < 0 else 2.0)
        if command.startswith(("repair ", "appraise ", "get ")) and not any(x in text for x in ("cannot", "not found", "do not")):
            reward += 5.0
    elif phase == 6:  # Progression
        if after.level > before.level:
            reward += 500.0
        if after.exp > before.exp:
            reward += min(250.0, float(after.exp - before.exp))
        if command.startswith("train") and not any(x in text for x in ("cannot", "not enough", "unavailable:")):
            reward += 15.0
    else:  # Economy & Macro Strategy
        wealth_delta = after.wealth_copper - before.wealth_copper
        if wealth_delta > 0:
            reward += wealth_delta / 100.0
        if gear_name and command.lower().startswith(("equip ", "wear ", "wield ")) and any(x in text for x in ("you equip", "you wear", "you wield")):
            delta = gear_metric(after) - gear_metric(before)
            learner.gear[gear_name.lower()] = learner.gear.get(gear_name.lower(), 0.0) * 0.7 + delta * 0.3
            reward += 100.0 if delta > 0 else (-25.0 if delta < 0 else 2.0)
        reserve = reserve_gold * 100
        if command.lower().startswith("deposit") and "you deposit" in text:
            reward += 25.0
        if after.wealth_copper < reserve and command in ("look", "score", "inventory", "services", "bank", "wealth"):
            reward -= 5.0

    if any(x in text for x in ("unavailable:", "there is no generated", "cannot ", "could not ", "do not carry", "not found")):
        reward -= 2.0
    return reward, survival_bonus_due


def phase_schedule(config: dict[str, Any], finite_hours: float | None = None) -> list[tuple[str, float]]:
    raw: list[tuple[str, float]] = []
    for item in config.get("curriculum", []):
        name = str(item.get("name", "Training"))
        if "seconds" in item:
            seconds = float(item["seconds"])
        elif "minutes" in item:
            seconds = float(item["minutes"]) * 60.0
        else:
            seconds = float(item.get("hours", 0.5)) * 3600.0
        raw.append((name, max(1.0, seconds)))
    if not raw:
        raise RuntimeError("Training curriculum is empty")
    if finite_hours is not None:
        base = sum(seconds for _, seconds in raw)
        target = max(1.0, finite_hours * 3600.0)
        scale = target / base
        raw = [(name, seconds * scale) for name, seconds in raw]
    return raw


def should_stop(stop_flag: bool, stop_request: Path) -> bool:
    return stop_flag or stop_request.exists()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--continuous", action="store_true", help="repeat the curriculum until stopped")
    ap.add_argument("--hours", type=float, default=None, help="optional finite diagnostic run length")
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

    continuous = bool(args.continuous or (args.hours is None and config.get("continuous", True)))
    reserve_gold = int(config.get("economy", {}).get("carried_gold_reserve", 10))
    action_delay = float(config.get("action_delay_seconds", 0.15))
    checkpoint_seconds = float(config.get("checkpoint_seconds", 60))
    status_seconds = float(config.get("status_seconds", 15))
    restart_delay = float(config.get("runtime_restart_delay_seconds", 2.0))
    parity_config = config.get("parity_regression", {}) if isinstance(config.get("parity_regression", {}), dict) else {}
    parity_enabled = bool(parity_config.get("enabled", True))
    parity_run_startup = bool(parity_config.get("run_on_startup", True))
    parity_run_each_cycle = bool(parity_config.get("run_each_cycle", True))
    parity_timeout = float(parity_config.get("gate_timeout_seconds", 300.0))
    parity_gates = [str(x) for x in parity_config.get("gates", DEFAULT_PARITY_GATES) if str(x).strip()]
    parity_player_probes = [str(x) for x in parity_config.get("player_visible_probes", PLAYER_VISIBLE_PARITY_PROBES) if str(x).strip()]

    runtime.mkdir(parents=True, exist_ok=True)
    pid_path = runtime / "trainer.pid"
    stop_request = runtime / "stop.request"
    stop_request.unlink(missing_ok=True)
    pid_path.write_text(str(os.getpid()), encoding="utf-8")
    log_path = runtime / "trainer.log"
    learner = QLearner(checkpoint)
    tester = TrainingTester(runtime / "defects.json", runtime / "defects.md", config.get("tester", {}))
    stop = False

    def on_stop(*_: Any) -> None:
        nonlocal stop
        stop = True

    signal.signal(signal.SIGINT, on_stop)
    signal.signal(signal.SIGTERM, on_stop)

    started = time.time()
    last_checkpoint = started
    last_status = 0.0
    last_survival_bonus = started
    visited_rooms: set[str] = set(learner.visits)
    schedule = phase_schedule(config, None if continuous else args.hours)
    cycle_seconds = sum(seconds for _, seconds in schedule)
    client: HeadlessClient | None = None
    last_shop: list[tuple[str, int]] = []
    cycle = 1
    phase_name = "Starting headless runtime"
    phase_started = started
    last_command = ""
    state: dict[str, Any] = {}
    parity_status: dict[str, Any] = {
        "enabled": parity_enabled,
        "scorecardPassed": False,
        "scorecardDetail": "not checked",
        "gates": [],
        "playerProbes": [],
        "lastCheckedUtc": None,
    }

    def run_parity_regression(
        current_cycle: int,
        current_client: HeadlessClient | None = None,
        *,
        run_gates: bool = True,
        run_player_probes: bool = True,
    ) -> None:
        nonlocal parity_status
        if not parity_enabled:
            return
        scorecard_passed, scorecard_detail = validate_parity_scorecard(repo)
        if not scorecard_passed:
            tester.record(
                "critical", "classic-parity-scorecard", "100% Classic parity scorecard no longer matches 0369",
                "majormud_parity_scorecard.json", "preflight", "100% Classic parity regression", current_cycle, scorecard_detail,
            )

        existing_gates = parity_status.get("gates", []) if isinstance(parity_status.get("gates"), list) else []
        gate_results = existing_gates
        if run_gates:
            gate_results = [run_parity_smoke(repo, gate, parity_timeout, log_path) for gate in parity_gates]
            for result in gate_results:
                tester.observe_parity_gate(result, current_cycle)

        existing_probes = parity_status.get("playerProbes", []) if isinstance(parity_status.get("playerProbes"), list) else []
        player_results = existing_probes
        if run_player_probes and current_client is not None:
            player_results = []
            for probe in parity_player_probes:
                try:
                    probe_state = current_client.send(probe)
                    probe_passed = tester.observe_player_parity_probe(probe, probe_state, current_cycle)
                    response = clean(str(probe_state.get("response", ""))).strip()
                    player_results.append({
                        "command": probe,
                        "passed": probe_passed,
                        "response": response[:300],
                    })
                except Exception as exc:
                    tester.record("high", "parity-player-probe", "Player-visible parity probe failed to execute", probe, "unknown", "100% Classic parity regression", current_cycle, str(exc))
                    player_results.append({"command": probe, "passed": False, "response": str(exc)[:300]})

        parity_status = {
            "enabled": True,
            "scorecardPassed": scorecard_passed,
            "scorecardDetail": scorecard_detail,
            "gates": gate_results,
            "playerProbes": player_results,
            "passed": scorecard_passed and bool(gate_results) and all(bool(x.get("passed")) for x in gate_results) and bool(player_results) and all(bool(x.get("passed")) for x in player_results),
            "lastCheckedUtc": utc_now(),
        }
        write_json_atomic(runtime / "parity_regression.json", parity_status)
        tester.save(force=True)

    def write_status(current_state: dict[str, Any], current_phase: str, phase_index: int,
                     phase_duration: float, phase_start: float, command: str, response: str) -> None:
        now = time.time()
        snap = numeric_snapshot(current_state)
        bank_text = clean(str(current_state.get("bank", "")))
        carried_copper = carried_copper_from_state(current_state)
        banked_gold: str | float = "unknown"
        m = re.search(r"Banked:\s*(.*)", bank_text, re.I)
        if m:
            banked_gold = m.group(1).strip()
        tester_summary = tester.summary()
        last_defect = tester.last_defect()
        phase_elapsed = max(0.0, now - phase_start)
        payload = {
            "mode": "continuous" if continuous else "finite",
            "cycle": cycle,
            "completedCycles": max(0, cycle - 1),
            "phaseIndex": phase_index + 1,
            "phaseCount": len(schedule),
            "phase": current_phase,
            "elapsed": f"{(now-started)/3600:.2f}h",
            "remaining": "until stopped" if continuous else "finite run",
            "phaseElapsed": f"{phase_elapsed/60:.1f}m",
            "phaseRemaining": f"{max(0.0, phase_duration-phase_elapsed)/60:.1f}m",
            "cycleDuration": f"{cycle_seconds/3600:.2f}h",
            "steps": learner.steps,
            "totalReward": round(learner.total_reward, 2),
            "epsilon": round(learner.epsilon, 4),
            "room": snap.room,
            "level": snap.level,
            "carriedGold": round(carried_copper / 100.0, 2),
            "bankedGold": banked_gold,
            "reserveRule": f"Keep {reserve_gold} gold carried; bank surplus; never withdraw to refill reserve.",
            "lastCommand": command,
            "lastResponse": clean(response)[:500],
            "tester": tester_summary,
            "parityRegression": parity_status,
            "lastDefect": last_defect,
            "updatedUtc": utc_now(),
        }
        write_json_atomic(runtime / "status.json", payload)

    try:
        write_json_atomic(runtime / "status.json", {
            "mode": "continuous" if continuous else "finite",
            "cycle": cycle,
            "phase": "Starting headless runtime",
            "elapsed": "0s",
            "remaining": "until stopped" if continuous else "finite run",
            "steps": learner.steps,
            "totalReward": learner.total_reward,
            "carriedGold": 0,
            "bankedGold": 0,
            "tester": tester.summary(),
            "parityRegression": parity_status,
            "updatedUtc": utc_now(),
        })
        if parity_enabled and parity_run_startup:
            phase_name = "100% Classic parity preflight"
            run_parity_regression(cycle, None, run_gates=True, run_player_probes=False)
        client = HeadlessClient(repo, args.player, log_path)
        state = client.state
        if parity_enabled and parity_run_startup:
            phase_name = "100% Classic player-visible parity probes"
            run_parity_regression(cycle, client, run_gates=False, run_player_probes=True)
            state = client.state

        while not should_stop(stop, stop_request):
            for phase_index, (phase_name, phase_duration) in enumerate(schedule):
                phase_started = time.time()
                while not should_stop(stop, stop_request) and time.time() - phase_started < phase_duration:
                    before = numeric_snapshot(state)
                    actions = PHASE_ACTIONS[min(phase_index, len(PHASE_ACTIONS) - 1)]
                    key = state_key(phase_index, state, before)
                    action_index = learner.choose(key, len(actions))
                    action = actions[action_index]

                    # Hard economy policy: preserve ten gold, bank surplus, never withdraw.
                    if phase_index == 7:
                        carried = carried_copper_from_state(state)
                        bank_text_now = clean(str(state.get("bank", ""))).lower()
                        if carried > reserve_gold * 100 and "carried:" in bank_text_now:
                            deposit_cmd = reserve_deposit_command(state, reserve_gold)
                            if deposit_cmd != "look":
                                action = "deposit_surplus"
                                action_index = actions.index(action)
                        elif carried < reserve_gold * 100:
                            # Earn/find/sell instead of withdrawing or buying.
                            if first_npc_name(state):
                                action = "attack"
                            elif inventory_items(str(state.get("inventory", ""))) and "no generated services" not in clean(str(state.get("services", ""))).lower():
                                action = "sell_candidate"
                            else:
                                action = random.choice(["get_all", "explore"])
                            action_index = actions.index(action)

                    command, gear_name = command_for(action, phase_index, state, learner, reserve_gold, last_shop)
                    last_command = command
                    try:
                        next_state = client.send(command)
                    except Exception as exc:
                        tester.record_restart(command, before.room, phase_name, cycle, exc)
                        tester.save(force=True)
                        learner.save()
                        if client:
                            client.close()
                        time.sleep(max(0.1, restart_delay))
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
                    learner.update(key, action_index, reward, next_key, len(actions))
                    tester.observe(before, after, next_state, command, phase_name, cycle)
                    state = next_state

                    if after.dead:
                        state = client.reset_session()

                    now = time.time()
                    if now - last_checkpoint >= checkpoint_seconds:
                        learner.save()
                        tester.save(force=True)
                        last_checkpoint = now
                    if now - last_status >= status_seconds:
                        write_status(state, phase_name, phase_index, phase_duration, phase_started, command, str(state.get("response", "")))
                        tester.save()
                        last_status = now
                    if action_delay > 0:
                        time.sleep(action_delay)

                if should_stop(stop, stop_request):
                    break

            if should_stop(stop, stop_request):
                break
            if not continuous:
                break
            if parity_enabled and parity_run_each_cycle:
                phase_name = "100% Classic parity regression"
                if client is not None:
                    client.close()
                    client = None
                run_parity_regression(cycle, None, run_gates=True, run_player_probes=False)
                client = HeadlessClient(repo, args.player, log_path)
                state = client.state
                run_parity_regression(cycle, client, run_gates=False, run_player_probes=True)
                state = client.state
            cycle += 1

        learner.save()
        tester.save(force=True)
        elapsed = time.time() - started
        final_state = state or {}
        snap = numeric_snapshot(final_state)
        write_json_atomic(runtime / "status.json", {
            "mode": "continuous" if continuous else "finite",
            "cycle": cycle,
            "phase": "Stopped" if should_stop(stop, stop_request) else "Complete",
            "elapsed": f"{elapsed/3600:.2f}h",
            "remaining": "stopped" if should_stop(stop, stop_request) else "0h",
            "steps": learner.steps,
            "totalReward": round(learner.total_reward, 2),
            "carriedGold": round(carried_copper_from_state(final_state) / 100.0, 2) if final_state else 0,
            "bankedGold": "see last bank snapshot",
            "tester": tester.summary(),
            "parityRegression": parity_status,
            "lastDefect": tester.last_defect(),
            "lastCommand": last_command,
            "room": snap.room,
            "updatedUtc": utc_now(),
        })
        return 0
    except Exception as exc:
        tester.record("critical", "trainer-fault", "Trainer process faulted", last_command, numeric_snapshot(state).room if state else "unknown", phase_name, cycle, str(exc))
        tester.save(force=True)
        write_json_atomic(runtime / "status.json", {
            "mode": "continuous" if continuous else "finite",
            "cycle": cycle,
            "phase": "Faulted",
            "elapsed": f"{(time.time()-started)/3600:.2f}h",
            "remaining": "unknown",
            "steps": learner.steps,
            "totalReward": round(learner.total_reward, 2),
            "carriedGold": "unknown",
            "bankedGold": "unknown",
            "tester": tester.summary(),
            "parityRegression": parity_status,
            "error": str(exc),
            "updatedUtc": utc_now(),
        })
        with log_path.open("a", encoding="utf-8") as log:
            log.write(f"TRAINER ERROR: {exc}\n")
        return 1
    finally:
        try:
            learner.save()
            tester.save(force=True)
        except Exception:
            pass
        if client:
            client.close()
        try:
            pid_path.unlink(missing_ok=True)
            stop_request.unlink(missing_ok=True)
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
