"""Runtime-0015 World Runtime Integration.

This module is the bridge between the offline runtime subsystems and the future
server/host layer. It does not add networking. Instead it proves that a world can
be loaded, populated, commanded, ticked, and saved as one integrated runtime.

Classic accuracy note: this is a Reforged runtime integration shell. It does not
claim to reproduce hidden classic timing, combat, AI, or scripting formulas. Those
remain evidence-gated in each subsystem.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional

from Runtime.data_runtime import DataRuntime
from Runtime.game_loop.runtime_kernel import RuntimeKernel, RuntimeKernelConfig
from Runtime.persistence.persistence_runtime import PersistenceRuntime
from Runtime.world.world_state import RuntimeEntity, GroundItem, WorldState


@dataclass
class WorldRuntimeConfig:
    world_id: str = "runtime_0015_offline_world"
    mode: str = "reforged_formula"
    classic_accuracy: str = "runtime_integration_not_classic_claimed"
    autosave_every_ticks: int = 10
    seed: int = 15015
    max_spawn_monsters: int = 3
    max_ground_items: int = 3


@dataclass
class OfflineWorldRunResult:
    world_id: str
    ticks_run: int
    commands_run: int
    events_before: int
    events_after: int
    players: int
    monsters: int
    corpses: int
    ground_items: int
    save_paths: List[str] = field(default_factory=list)
    command_results: List[Dict[str, Any]] = field(default_factory=list)
    trace: List[Dict[str, Any]] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "world_id": self.world_id,
            "ticks_run": self.ticks_run,
            "commands_run": self.commands_run,
            "events_before": self.events_before,
            "events_after": self.events_after,
            "players": self.players,
            "monsters": self.monsters,
            "corpses": self.corpses,
            "ground_items": self.ground_items,
            "save_paths": self.save_paths,
            "command_results": self.command_results,
            "trace": self.trace,
        }


class WorldRuntime:
    """Offline world driver used by Runtime-0015.

    Responsibilities:
    - load canonical data
    - create or load an in-memory WorldState
    - seed a playable offline world with one player, monsters, and ground items
    - run commands through the RuntimeKernel dispatcher
    - advance all tick tasks through the scheduler
    - save world and character snapshots through Runtime-0014 persistence
    """

    def __init__(self, repo_root: str | Path, config: Optional[WorldRuntimeConfig] = None):
        self.repo_root = Path(repo_root)
        self.config = config or WorldRuntimeConfig()
        self.data = DataRuntime.from_repo_root(self.repo_root).load()
        self.kernel = RuntimeKernel(
            repo_root=self.repo_root,
            data_runtime=self.data,
            config=RuntimeKernelConfig(mode=self.config.mode, classic_accuracy=self.config.classic_accuracy),
        )
        self.kernel.world.world_id = self.config.world_id
        self.persistence = PersistenceRuntime(self.repo_root / "Saves")
        self.trace: List[Dict[str, Any]] = []

    def load_or_create_world(self) -> WorldState:
        """Create a deterministic offline world suitable for validation.

        Future server milestones can call PersistenceRuntime.load_world first and
        assign the loaded state to kernel.world. Runtime-0015 keeps creation
        deterministic so phone handoffs and validation produce stable results.
        """
        if self.kernel.world.players:
            return self.kernel.world
        self.kernel.create_demo_world()
        self.kernel.world.world_id = self.config.world_id
        self._seed_data_backed_monsters()
        self._seed_data_backed_ground_items()
        self.kernel.world.flags["runtime_0015_loaded"] = True
        self.kernel.world.add_event(
            "world_runtime_loaded",
            message="Runtime-0015 offline world loaded and seeded.",
            data={"world_id": self.config.world_id, "classicAccuracy": self.config.classic_accuracy},
        )
        self.trace.append({"event": "world_loaded", "world_id": self.config.world_id})
        return self.kernel.world

    def _safe_name(self, record: Dict[str, Any], fallback: str) -> str:
        for key in ("name", "Name", "title", "Title", "text", "Text"):
            value = record.get(key)
            if value:
                return str(value)
        return fallback

    def _record_id(self, record: Dict[str, Any], fallback: str) -> str:
        for key in ("id", "ID", "number", "Number", "monster", "Monster", "item", "Item"):
            value = record.get(key)
            if value is not None:
                return str(value)
        return fallback

    def _start_room(self) -> Any:
        players = list(self.kernel.world.players.values())
        if players:
            return players[0].room_ref
        return (1, 1)

    def _seed_data_backed_monsters(self) -> None:
        room_ref = self._start_room()
        existing = len(self.kernel.world.monsters)
        records = self.data.get_records("monsters")[: self.config.max_spawn_monsters]
        for idx, rec in enumerate(records):
            runtime_id = f"data_monster_{idx}_{self._record_id(rec, str(idx))}"
            if runtime_id in self.kernel.world.monsters:
                continue
            level = int(rec.get("level") or rec.get("Level") or 1) if str(rec.get("level") or rec.get("Level") or "1").isdigit() else 1
            hp = int(rec.get("hp") or rec.get("HP") or rec.get("hit_points") or 10) if str(rec.get("hp") or rec.get("HP") or rec.get("hit_points") or "10").isdigit() else 10
            monster = RuntimeEntity(
                runtime_id=runtime_id,
                name=self._safe_name(rec, f"Data Monster {idx}"),
                entity_type="monster",
                room_ref=room_ref,
                hp=max(1, min(hp, 50)),
                max_hp=max(1, min(hp, 50)),
                level=max(1, min(level, 50)),
                stats={"str": 45 + idx, "agl": 40 + idx, "hea": 45 + idx},
                skills={"natural": 10 + idx},
                equipment_totals={"min_hit": 1, "max_hit": 3 + idx, "armor_class_bonus": 0, "damage_resistance_bonus": 0},
                flags={"source": "Data/monsters.json", "source_id": self._record_id(rec, str(idx)), "classicAccuracy": "data_backed_identity_runtime_formula_unverified"},
            )
            self.kernel.world.add_monster(monster)
        self.trace.append({"event": "monsters_seeded", "before": existing, "after": len(self.kernel.world.monsters)})

    def _seed_data_backed_ground_items(self) -> None:
        room_ref = self._start_room()
        records = self.data.get_records("items")[: self.config.max_ground_items]
        for idx, rec in enumerate(records):
            item_id = f"data_item_{idx}_{self._record_id(rec, str(idx))}"
            if any(g.item_id == item_id for g in self.kernel.world.ground_items):
                continue
            self.kernel.world.ground_items.append(
                GroundItem(
                    item_id=item_id,
                    name=self._safe_name(rec, f"Data Item {idx}"),
                    room_ref=room_ref,
                    data={"source": "Data/items.json", "source_id": self._record_id(rec, str(idx)), "classicAccuracy": "data_backed_identity_runtime_behavior_unverified"},
                )
            )
        self.trace.append({"event": "ground_items_seeded", "count": len(self.kernel.world.ground_items)})

    def submit_commands(self, actor_id: str, commands: Iterable[str]) -> List[Dict[str, Any]]:
        results: List[Dict[str, Any]] = []
        for raw in commands:
            result = self.kernel.submit_command(actor_id, raw)
            result_dict = result.to_dict() if hasattr(result, "to_dict") else dict(result.__dict__)
            result_dict["raw"] = raw
            results.append(result_dict)
            self.trace.append({"event": "command_executed", "actor_id": actor_id, "raw": raw, "ok": bool(result_dict.get("ok"))})
        return results

    def run_offline_script(self, commands: Optional[List[str]] = None, ticks: int = 12, actor_id: str = "player_runtime_tester") -> OfflineWorldRunResult:
        self.load_or_create_world()
        events_before = len(self.kernel.world.events)
        default_commands = ["look", "inventory", "get runtime", "attack goblin", "cast heal Runtime", "say Runtime 0015 online", "who"]
        command_results = self.submit_commands(actor_id, commands or default_commands)
        self.kernel.tick(ticks)
        save_paths = self.autosave("runtime_0015_offline_script")
        world = self.kernel.world
        result = OfflineWorldRunResult(
            world_id=world.world_id,
            ticks_run=ticks,
            commands_run=len(command_results),
            events_before=events_before,
            events_after=len(world.events),
            players=len(world.players),
            monsters=len(world.monsters),
            corpses=len(world.corpses),
            ground_items=len(world.ground_items),
            save_paths=[str(p) for p in save_paths],
            command_results=command_results,
            trace=list(self.trace),
        )
        world.flags["last_offline_run"] = result.to_dict()
        return result

    def autosave(self, snapshot_id: str) -> List[Path]:
        paths: List[Path] = []
        paths.append(self.persistence.save_world(self.kernel.world))
        for player in self.kernel.world.players.values():
            paths.append(self.persistence.save_character(player))
        paths.append(self.persistence.save_snapshot(snapshot_id, self.kernel.world))
        self.trace.append({"event": "autosave_complete", "paths": [str(p) for p in paths]})
        return paths

    def summary(self) -> Dict[str, Any]:
        summary = self.kernel.summary()
        summary.update({
            "milestone": "Runtime-0015",
            "world_id": self.kernel.world.world_id,
            "data_counts": {name: self.data.count(name) for name in ("rooms", "items", "monsters", "spells", "shops", "classes", "races", "abilities", "quests")},
            "world_runtime_trace": self.trace,
        })
        return summary
