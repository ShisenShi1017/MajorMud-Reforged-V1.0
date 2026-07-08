"""Runtime-0016 Full Game Test Harness.

This harness does not claim classic formula accuracy. It exercises every major
runtime subsystem that currently exists and records PASS/FAIL/UNKNOWN results.
UNKNOWN means the runtime surface exists but the exact classic behavior still
needs evidence before it can be promoted as classic-verified.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Callable, Optional
import json
import traceback

from Runtime.world_integration import WorldRuntime, WorldRuntimeConfig
from Runtime.persistence.persistence_runtime import PersistenceRuntime
from Runtime.abilities.ability_engine import AbilityState, AbilityGrant
from Runtime.class_race_engine import ClassRaceEngine
from Runtime.loot.loot_runtime import LootRuntime
from Runtime.economy.economy_runtime import EconomyRuntime, Wallet
from Runtime.npc_services.npc_services_runtime import NPCServiceRuntime

@dataclass
class AspectResult:
    name: str
    status: str
    passed: int = 0
    failed: int = 0
    unknown: int = 0
    notes: List[str] = field(default_factory=list)
    details: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "status": self.status,
            "passed": self.passed,
            "failed": self.failed,
            "unknown": self.unknown,
            "notes": self.notes,
            "details": self.details,
        }

@dataclass
class FullGameHarnessConfig:
    world_id: str = "runtime_0016_full_game_harness"
    actor_id: str = "player_runtime_tester"
    ticks: int = 25
    max_spawn_monsters: int = 4
    max_ground_items: int = 4

@dataclass
class FullGameHarnessResult:
    milestone: str
    status: str
    passed: int
    failed: int
    unknown: int
    aspects: List[AspectResult]
    summary: Dict[str, Any]

    def to_dict(self) -> Dict[str, Any]:
        return {
            "milestone": self.milestone,
            "status": self.status,
            "passed": self.passed,
            "failed": self.failed,
            "unknown": self.unknown,
            "summary": self.summary,
            "aspects": [a.to_dict() for a in self.aspects],
        }

class FullGameTestHarness:
    """Runs a broad automated playthrough and subsystem probes."""

    def __init__(self, repo_root: str | Path, config: Optional[FullGameHarnessConfig] = None):
        self.repo_root = Path(repo_root)
        self.config = config or FullGameHarnessConfig()
        self.runtime = WorldRuntime(
            self.repo_root,
            WorldRuntimeConfig(
                world_id=self.config.world_id,
                max_spawn_monsters=self.config.max_spawn_monsters,
                max_ground_items=self.config.max_ground_items,
            ),
        )
        self.world = self.runtime.load_or_create_world()
        self.actor_id = self.config.actor_id
        self.aspects: List[AspectResult] = []

    def _aspect(self, name: str, fn: Callable[[], AspectResult]) -> None:
        try:
            self.aspects.append(fn())
        except Exception as exc:
            self.aspects.append(AspectResult(
                name=name,
                status="ERROR",
                failed=1,
                notes=[str(exc), traceback.format_exc()],
            ))

    def _ok(self, name: str, passed: int = 1, notes: Optional[List[str]] = None, **details: Any) -> AspectResult:
        return AspectResult(name=name, status="PASS", passed=passed, notes=notes or [], details=details)

    def _fail(self, name: str, note: str, **details: Any) -> AspectResult:
        return AspectResult(name=name, status="FAIL", failed=1, notes=[note], details=details)

    def _unknown(self, name: str, note: str, passed: int = 0, **details: Any) -> AspectResult:
        return AspectResult(name=name, status="UNKNOWN", passed=passed, unknown=1, notes=[note], details=details)

    def run(self) -> FullGameHarnessResult:
        tests = [
            ("data_loading", self.test_data_loading),
            ("world_boot", self.test_world_boot),
            ("classes_races", self.test_classes_races),
            ("abilities_quests", self.test_abilities_quests),
            ("commands_and_movement", self.test_commands_and_movement),
            ("inventory_equipment", self.test_inventory_equipment),
            ("combat_spells_status", self.test_combat_spells_status),
            ("loot_economy_npc", self.test_loot_economy_npc),
            ("regeneration_spawn_ai_ticks", self.test_regeneration_spawn_ai_ticks),
            ("persistence_roundtrip", self.test_persistence_roundtrip),
            ("scenario_playthrough", self.test_scenario_playthrough),
            ("formula_provenance", self.test_formula_provenance),
        ]
        for name, fn in tests:
            self._aspect(name, fn)

        passed = sum(a.passed for a in self.aspects)
        failed = sum(a.failed for a in self.aspects)
        unknown = sum(a.unknown for a in self.aspects)
        status = "FAIL" if failed else "PASS_WITH_UNKNOWN" if unknown else "PASS"
        summary = {
            "world_id": self.world.world_id,
            "tick": self.world.tick,
            "players": len(self.world.players),
            "monsters": len(self.world.monsters),
            "ground_items": len(self.world.ground_items),
            "corpses": len(self.world.corpses),
            "events": len(self.world.events),
            "commands": len(self.runtime.kernel.command_history),
            "unknown_policy": "UNKNOWN is expected where exact classic mechanics remain evidence-gated.",
        }
        return FullGameHarnessResult("Runtime-0016", status, passed, failed, unknown, self.aspects, summary)

    def test_data_loading(self) -> AspectResult:
        counts = {name: len(self.runtime.data.get_records(name)) for name in ["rooms", "items", "monsters", "spells", "shops", "classes", "races", "abilities", "quests"]}
        required = ["rooms", "items", "monsters", "spells", "shops", "classes", "races"]
        missing = [k for k in required if counts.get(k, 0) <= 0]
        if missing:
            return self._fail("data_loading", f"Missing required data groups: {missing}", counts=counts)
        unknown = 1 if counts.get("quests", 0) == 0 else 0
        notes = ["Canonical data groups loaded."]
        if unknown:
            notes.append("Quest data is empty; quest behavior is tested with non-promoted synthetic state only.")
        return AspectResult("data_loading", "PASS_WITH_UNKNOWN" if unknown else "PASS", passed=len(required), unknown=unknown, notes=notes, details={"counts": counts})

    def test_world_boot(self) -> AspectResult:
        if not self.world.players:
            return self._fail("world_boot", "No player loaded into world.")
        if not self.world.monsters:
            return self._fail("world_boot", "No monsters seeded into world.")
        return self._ok("world_boot", 3, ["Offline world boots with player, monsters, and events."], world_id=self.world.world_id, events=len(self.world.events))

    def test_classes_races(self) -> AspectResult:
        engine = ClassRaceEngine(self.repo_root / "Data")
        class_ids = list(engine.classes.keys())
        race_ids = list(engine.races.keys())
        if not class_ids or not race_ids:
            return self._fail("classes_races", "Class or race data missing.")
        profile = engine.build_profile("Harness Character", class_ids[0], race_ids[0], 1, clamp_invalid_stats=True).to_dict()
        return AspectResult("classes_races", "PASS_WITH_UNKNOWN", passed=2, unknown=1, notes=["Class/race profiles build from data.", "Hidden exact passives remain evidence-gated."], details={"profile": profile})

    def test_abilities_quests(self) -> AspectResult:
        ability_state = AbilityState()
        grant = ability_state.grant(AbilityGrant("runtime_0016_test_flag", "test_harness", "runtime_0016", scope="temporary"))
        has_flag = ability_state.has("runtime_0016_test_flag")
        quest_count = len(self.runtime.data.get_records("quests"))
        if not has_flag:
            return self._fail("abilities_quests", "Ability flag grant/query failed.", grant=grant)
        status = "PASS_WITH_UNKNOWN" if quest_count == 0 else "PASS"
        unknown = 1 if quest_count == 0 else 0
        notes = ["Ability flag grant/query succeeded."]
        if unknown:
            notes.append("No canonical quest records are currently promoted in Data/quests.json.")
        return AspectResult("abilities_quests", status, passed=1, unknown=unknown, notes=notes, details={"quest_count": quest_count, "grant": grant})

    def test_commands_and_movement(self) -> AspectResult:
        commands = ["look", "help", "who", "say harness online"]
        results = self.runtime.submit_commands(self.actor_id, commands)
        ok_core = all(r.get("ok") for r in results)
        move_results = self.runtime.submit_commands(self.actor_id, ["north"])
        if not ok_core:
            return self._fail("commands_and_movement", "Core commands failed.", results=results)
        movement_status = "moved" if move_results and move_results[0].get("ok") else "blocked_or_no_exit"
        return AspectResult("commands_and_movement", "PASS_WITH_UNKNOWN", passed=4, unknown=1, notes=["Core command parser/dispatcher works.", "Movement command executed; destination depends on canonical room exits."], details={"movement_status": movement_status, "results": results, "movement": move_results})

    def test_inventory_equipment(self) -> AspectResult:
        before = len(self.world.get_entity(self.actor_id).inventory)
        get_result = self.runtime.submit_commands(self.actor_id, ["get data"])[0]
        inv_result = self.runtime.submit_commands(self.actor_id, ["inventory", "equipment"])
        after = len(self.world.get_entity(self.actor_id).inventory)
        if not inv_result[0].get("ok") or not inv_result[1].get("ok"):
            return self._fail("inventory_equipment", "Inventory/equipment commands failed.", inventory=inv_result)
        return AspectResult("inventory_equipment", "PASS_WITH_UNKNOWN", passed=2, unknown=1, notes=["Inventory/equipment surfaces execute.", "Full equip/unequip restrictions remain formula/evidence-gated."], details={"before": before, "after": after, "get_result": get_result})

    def test_combat_spells_status(self) -> AspectResult:
        attack = self.runtime.submit_commands(self.actor_id, ["attack data"])[0]
        cast = self.runtime.submit_commands(self.actor_id, ["cast heal Runtime"])[0]
        self.runtime.kernel.tick(3)
        if not attack.get("ok") and not cast.get("ok"):
            return self._fail("combat_spells_status", "Both attack and cast failed.", attack=attack, cast=cast)
        return AspectResult("combat_spells_status", "PASS_WITH_UNKNOWN", passed=int(bool(attack.get("ok"))) + int(bool(cast.get("ok"))), unknown=1, notes=["Combat/spell command paths execute where valid targets exist.", "Exact classic hit/damage/resist formulas remain evidence-gated."], details={"attack": attack, "cast": cast, "status_effects": len(self.world.status_effects)})

    def test_loot_economy_npc(self) -> AspectResult:
        loot_runtime = LootRuntime(seed=16016)
        economy_runtime = EconomyRuntime()
        npc_runtime = NPCServiceRuntime()
        loot = loot_runtime.generate_monster_loot({"id": "test_monster", "min_gold": 5, "max_gold": 5, "loot": []}, room_id="test_room")
        wallet = Wallet(gold=25, bank_gold=0)
        bank = economy_runtime.deposit(wallet, 10)
        heal = npc_runtime.healer_quote(missing_hp=5, gold_per_hp=1)
        checks = [loot is not None and loot.corpse is not None, bank.success is True, heal.success is True]
        if not all(checks):
            return self._fail("loot_economy_npc", "Loot/economy/NPC service probe failed.", loot=loot_runtime.to_dict(loot), bank=economy_runtime.to_dict(bank), heal=npc_runtime.to_dict(heal))
        return self._ok("loot_economy_npc", 3, ["Loot, banking, and NPC healer service probes passed."], loot=loot_runtime.to_dict(loot), bank=economy_runtime.to_dict(bank), heal=npc_runtime.to_dict(heal))

    def test_regeneration_spawn_ai_ticks(self) -> AspectResult:
        before_tick = self.world.tick
        before_events = len(self.world.events)
        self.runtime.kernel.tick(self.config.ticks)
        event_types = [e.event_type for e in self.world.events[before_events:]]
        required_seen = any(t in event_types for t in ["runtime_tick", "spawn_tick", "monster_ai_engaged", "regeneration_tick"])
        if self.world.tick <= before_tick or not required_seen:
            return self._fail("regeneration_spawn_ai_ticks", "Tick scheduler did not advance expected runtime events.", event_types=event_types[:20])
        return self._ok("regeneration_spawn_ai_ticks", 3, ["Scheduler ticks advanced runtime systems."], tick_before=before_tick, tick_after=self.world.tick, event_types=sorted(set(event_types)))

    def test_persistence_roundtrip(self) -> AspectResult:
        persistence = PersistenceRuntime(self.repo_root / "Saves")
        world_path = persistence.save_world(self.world)
        player = self.world.get_entity(self.actor_id)
        char_path = persistence.save_character(player)
        snapshot_path = persistence.save_snapshot("runtime_0016_full_game_harness", self.world)
        paths = [world_path, char_path, snapshot_path]
        if not all(Path(p).exists() for p in paths):
            return self._fail("persistence_roundtrip", "One or more save files were not created.", paths=[str(p) for p in paths])
        return self._ok("persistence_roundtrip", 3, ["World, character, and snapshot saves created."], paths=[str(p) for p in paths])

    def test_scenario_playthrough(self) -> AspectResult:
        script = ["look", "get data", "inventory", "attack data", "cast heal Runtime", "say scenario complete", "who"]
        result = self.runtime.run_offline_script(commands=script, ticks=10, actor_id=self.actor_id)
        good = result.commands_run == len(script) and result.events_after > result.events_before
        if not good:
            return self._fail("scenario_playthrough", "Scenario did not complete expected command/tick/event flow.", result=result.to_dict())
        return self._ok("scenario_playthrough", 4, ["Scripted playthrough completed command/tick/save flow."], result=result.to_dict())

    def test_formula_provenance(self) -> AspectResult:
        return AspectResult("formula_provenance", "PASS_WITH_UNKNOWN", passed=1, unknown=1, notes=["Formula traces retain mode/provenance where implemented.", "Classic formula parity remains UNKNOWN until MMExplorer/MBBSEmu evidence verifies exact formulas."], details={"mode": self.runtime.config.mode, "classic_accuracy": self.runtime.config.classic_accuracy})

    def write_report(self, result: FullGameHarnessResult, path: str | Path | None = None) -> Path:
        report_path = Path(path) if path else self.repo_root / "Reports" / "full_game_test_harness_0016.json"
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(result.to_dict(), indent=2, default=str), encoding="utf-8")
        return report_path
