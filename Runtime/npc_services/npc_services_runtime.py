"""Runtime-0010 NPC services runtime foundation."""
from __future__ import annotations
from dataclasses import dataclass, field, asdict
from typing import Any, Dict, List, Optional

@dataclass
class ServiceEvent:
    service_type: str
    success: bool
    cost: int = 0
    effect: Dict[str, Any] = field(default_factory=dict)
    notes: List[str] = field(default_factory=list)
    classic_accuracy: str = "not_classic_claimed"

class NPCServiceRuntime:
    def healer_quote(self, missing_hp: int, gold_per_hp: int = 1) -> ServiceEvent:
        cost = max(0, int(missing_hp)) * max(0, int(gold_per_hp))
        return ServiceEvent("healer_quote", True, cost=cost, effect={"heal_hp": max(0, int(missing_hp))}, notes=["Healer price is foundation default until classic evidence is promoted."])

    def inn_quote(self, level: int, base_cost: int = 10) -> ServiceEvent:
        cost = max(0, int(base_cost)) * max(1, int(level))
        return ServiceEvent("inn_quote", True, cost=cost, effect={"rest": True, "regen_tick_bonus": "full_rest_foundation"}, notes=["Inn/rest effect is scaffolded, not classic claimed."])

    def trainer_quote(self, level: int, skill_rank: int = 0, base_cost: int = 100) -> ServiceEvent:
        cost = max(0, int(base_cost)) * max(1, int(level)) * (max(0, int(skill_rank)) + 1)
        return ServiceEvent("trainer_quote", True, cost=cost, effect={"training_available": True}, notes=["Trainer cost is scaffolded, not classic claimed."])

    def guild_quote(self, service_name: str, base_cost: int = 0) -> ServiceEvent:
        return ServiceEvent("guild_service_quote", True, cost=max(0, int(base_cost)), effect={"service": service_name}, notes=["Guild service hook exists; concrete rules are future evidence work."])

    def travel_quote(self, destination_room_id: str, base_cost: int = 0) -> ServiceEvent:
        return ServiceEvent("travel_quote", True, cost=max(0, int(base_cost)), effect={"destination_room_id": destination_room_id}, notes=["Travel/teleport service hook exists; route rules are future evidence work."])

    def to_dict(self, event: ServiceEvent) -> Dict[str, Any]:
        return asdict(event)
