from __future__ import annotations
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from Runtime.command import CommandContext, CommandDispatcher, CommandParser, CommandRegistry, register_core_commands
from Runtime.game_loop.event_queue import EventQueue
from Runtime.game_loop.tick_scheduler import TickScheduler
from Runtime.world.world_state import WorldState, RuntimeEntity, GroundItem
from Runtime.movement import MovementRuntime
from Runtime.data_runtime import DataRuntime
from Runtime.spells.spell_runtime import StatusEffectRuntime, SpellRuntime

@dataclass
class RuntimeKernelConfig:
    tick_limit: int = 100
    mode: str = "reforged_formula"
    classic_accuracy: str = "not_classic_claimed"

class RuntimeKernel:
    """Runtime-0012 Core Runtime Kernel.

    Connects command dispatch, world state, event queue, tick scheduler, movement,
    combat hooks, spell/status ticks, regeneration hooks, AI hooks, spawn hooks, and cleanup.
    It is intentionally offline/local; networking and persistence are later milestones.
    """

    def __init__(self, repo_root=None, data_runtime: Optional[DataRuntime] = None, config: Optional[RuntimeKernelConfig] = None):
        self.config = config or RuntimeKernelConfig()
        self.repo_root = repo_root
        self.data_runtime = data_runtime or (DataRuntime(repo_root / "Data") if repo_root is not None else None)
        if self.data_runtime is not None and not getattr(self.data_runtime, "loaded", False):
            try:
                self.data_runtime.load_all()
            except Exception:
                pass
        self.world = WorldState()
        self.parser = CommandParser()
        self.registry = register_core_commands(CommandRegistry())
        self.dispatcher = CommandDispatcher(self.registry, self.parser)
        self.event_queue = EventQueue()
        self.scheduler = TickScheduler()
        self.status_runtime = StatusEffectRuntime()
        self.spell_runtime = SpellRuntime(seed=12012)
        self.movement_runtime = MovementRuntime(self.data_runtime) if self.data_runtime is not None else None
        self.command_history: List[Dict[str, Any]] = []
        self.running = False
        self._install_default_tasks()

    def _install_default_tasks(self) -> None:
        self.scheduler.add_task("event_queue", 1, 10, lambda k: k._tick_events())
        self.scheduler.add_task("monster_ai", 2, 20, lambda k: k._tick_monster_ai())
        self.scheduler.add_task("combat", 2, 30, lambda k: k._tick_combat())
        self.scheduler.add_task("status_effects", 1, 40, lambda k: k._tick_status_effects())
        self.scheduler.add_task("regeneration", 5, 50, lambda k: k._tick_regeneration())
        self.scheduler.add_task("spawn", 10, 60, lambda k: k._tick_spawn())
        self.scheduler.add_task("npc_services", 10, 70, lambda k: k._tick_npc_services())
        self.scheduler.add_task("cleanup", 10, 90, lambda k: k._tick_cleanup())

    def create_demo_world(self) -> "RuntimeKernel":
        # Find a room with a normal exit if possible; fallback to synthetic room ref.
        start_room: Any = (1, 1)
        if self.movement_runtime is not None:
            for room in self.data_runtime.get_records("rooms"):
                ref = self.movement_runtime.canonical_ref(room)
                if ref and self.movement_runtime.exits_for_room(ref):
                    start_room = ref
                    break
        player = RuntimeEntity("player_runtime_tester", "Runtime Tester", "player", room_ref=start_room, hp=40, max_hp=40, mana=20, max_mana=20, cp=10, max_cp=10, level=3, stats={"str":60,"agl":55,"hea":55,"intelligence":50}, skills={"weapon":25,"casting_skill":20}, equipment_totals={"min_hit":3,"max_hit":7,"armor_class_bonus":1,"damage_resistance_bonus":0})
        monster = RuntimeEntity("monster_runtime_goblin", "Runtime Goblin", "monster", room_ref=start_room, hp=18, max_hp=18, level=2, stats={"str":50,"agl":45,"hea":50}, skills={"natural":12}, equipment_totals={"min_hit":1,"max_hit":4,"armor_class_bonus":0,"damage_resistance_bonus":0})
        self.world.add_player(player)
        self.world.add_monster(monster)
        self.world.ground_items.append(GroundItem("runtime_coin", "runtime coin", start_room, data={"value":1}))
        self.world.flags["demo_world_created"] = True
        return self

    def context_for(self, actor_id: str) -> CommandContext:
        return CommandContext(world=self.world, actor_id=actor_id, runtime_kernel=self, data_runtime=self.data_runtime, movement_runtime=self.movement_runtime, spell_runtime=self.spell_runtime)

    def submit_command(self, actor_id: str, raw: str):
        ctx = self.context_for(actor_id)
        result = self.dispatcher.dispatch(ctx, raw)
        self.command_history.append({"tick": self.world.tick, "actor_id": actor_id, "raw": raw, "ok": result.ok, "message": result.message})
        return result

    def tick(self, count: int = 1) -> List[List[str]]:
        ran_all: List[List[str]] = []
        for _ in range(count):
            self.world.tick += 1
            ran = self.scheduler.run_due(self)
            self.world.add_event("runtime_tick", message="Runtime tick executed.", data={"tasks": ran})
            ran_all.append(ran)
        return ran_all

    def run_for_ticks(self, count: int) -> Dict[str, Any]:
        self.running = True
        self.tick(count)
        self.running = False
        return self.summary()

    def _tick_events(self):
        for event in self.event_queue.pop_due(self.world.tick):
            if event.handler:
                event.handler(self, event.payload)
            self.world.add_event("queued_event", message=event.event_type, data=event.payload)

    def _tick_monster_ai(self):
        for monster in list(self.world.monsters.values()):
            if not monster.alive:
                continue
            if monster.runtime_id in self.world.combat_pairs:
                continue
            players = [p for p in self.world.players.values() if p.room_ref == monster.room_ref and p.alive]
            if players:
                target = sorted(players, key=lambda p: p.runtime_id)[0]
                self.world.combat_pairs[monster.runtime_id] = target.runtime_id
                self.world.add_event("monster_ai_engaged", monster.runtime_id, target.runtime_id, message=f"{monster.name} engages {target.name}.")

    def _tick_combat(self):
        # Continuous combat exists as an event hook in 0012. Direct attack command executes attacks.
        stale = []
        for actor_id, target_id in self.world.combat_pairs.items():
            actor = self.world.get_entity(actor_id); target = self.world.get_entity(target_id)
            if actor is None or target is None or not actor.alive or not target.alive or actor.room_ref != target.room_ref:
                stale.append(actor_id)
        for key in stale:
            self.world.combat_pairs.pop(key, None)
            self.world.add_event("combat_pair_cleared", key)

    def _tick_status_effects(self):
        # merge command-applied effects into tracker, then tick them.
        for effect in self.world.status_effects:
            if effect not in self.status_runtime.effects:
                self.status_runtime.add(effect)
        for event in self.status_runtime.tick():
            self.world.add_event(event.get("event_type", "status_tick"), event.get("target_id"), data=event)
        expired = self.status_runtime.purge_expired()
        if expired:
            self.world.add_event("status_effects_expired", data={"count": expired})

    def _tick_regeneration(self):
        for entity in list(self.world.players.values()) + list(self.world.monsters.values()):
            if entity.alive:
                old_hp = entity.hp
                entity.hp = min(entity.max_hp, entity.hp + 1)
                if entity.hp != old_hp:
                    self.world.add_event("regeneration_tick", entity.runtime_id, data={"hp_before": old_hp, "hp_after": entity.hp})

    def _tick_spawn(self):
        self.world.add_event("spawn_tick", message="Spawn scheduler hook executed.")

    def _tick_npc_services(self):
        self.world.add_event("npc_services_tick", message="NPC service hook executed.")

    def _tick_cleanup(self):
        before = len(self.world.corpses)
        # Keep corpses for now; later persistence/decay can remove them.
        self.world.add_event("cleanup_tick", data={"corpses": before})

    def summary(self) -> Dict[str, Any]:
        return {
            "milestone": "Runtime-0012",
            "tick": self.world.tick,
            "players": len(self.world.players),
            "monsters": len(self.world.monsters),
            "events": len(self.world.events),
            "commands": len(self.command_history),
            "queued_events": len(self.event_queue),
            "tasks": self.scheduler.summary(),
            "mode": self.config.mode,
            "classic_accuracy": self.config.classic_accuracy,
        }
