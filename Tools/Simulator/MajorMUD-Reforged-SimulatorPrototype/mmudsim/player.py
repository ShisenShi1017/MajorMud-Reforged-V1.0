from __future__ import annotations

from dataclasses import dataclass, field
from .util import norm, as_int
from .world import WorldEngine, SpawnState


@dataclass
class PlayerState:
    name: str = "Tester"
    location: str = "ACCD"
    gold: int = 0
    hp: int = 100
    max_hp: int = 100
    xp: int = 0
    inventory: list[str] = field(default_factory=list)


class SimulationSession:
    """Stateful simulator session for movement, shopping, basic combat projection."""

    def __init__(self, world: WorldEngine, player: PlayerState | None = None):
        self.world = world
        self.player = player or PlayerState()
        self.spawns = SpawnState(world)

    def look(self) -> dict[str, object]:
        exits = self.world.exits(self.player.location)
        return {"location": self.player.location, "exits": exits}

    def move_to(self, to_code: str) -> bool:
        target = norm(to_code).upper()
        for edge in self.world.exits(self.player.location):
            if norm(edge.get("to_code")).upper() == target:
                self.player.location = target
                return True
        return False

    def buy(self, shop_id: str, item_text: str) -> str:
        _, inv = self.world.shop(shop_id)
        needle = norm(item_text).lower()
        for row in inv:
            if needle in norm(row.get("item_name")).lower() or needle == norm(row.get("item_id")).split(".")[0]:
                self.player.inventory.append(norm(row.get("item_name")) or norm(row.get("item_id")))
                return f"Bought {self.player.inventory[-1]} from shop {shop_id} (price not certified in RuntimePrep)."
        return f"Item not found in shop {shop_id}."

    def fight_projection(self, monster_ident: str) -> dict[str, object]:
        """Very small deterministic combat projection.

        Uses RuntimePrep0007 expected fields when present. This is a simulator smoke
        test, not a certified MajorMUD combat formula.
        """
        monster = self.world.monster(monster_ident)
        if not monster:
            return {"status": "missing_monster", "monster": monster_ident}
        hp = as_int(monster.get("hp"), 1)
        dmg_min = as_int(monster.get("damage_min"), 1)
        dmg_max = as_int(monster.get("damage_max"), dmg_min)
        monster_avg_damage = max(1, (dmg_min + dmg_max) // 2)
        player_damage = 10
        rounds = max(1, (hp + player_damage - 1) // player_damage)
        projected_damage_taken = rounds * monster_avg_damage
        return {
            "status": "projected_only_not_certified",
            "monster_id": norm(monster.get("monster_id")),
            "monster_name": norm(monster.get("name")),
            "rounds": rounds,
            "projected_damage_taken": projected_damage_taken,
            "xp_award_expected": as_int(monster.get("xp"), 0),
            "gold_award_expected": as_int(monster.get("gold"), 0),
            "boundary": "AI, formulas, attack timing, and rewards require live validation.",
        }
