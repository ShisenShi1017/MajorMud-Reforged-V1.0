from __future__ import annotations
from dataclasses import dataclass
from typing import Any

@dataclass
class CommandContext:
    world: Any
    actor_id: str
    runtime_kernel: Any = None
    data_runtime: Any = None
    movement_runtime: Any = None
    combat_runtime: Any = None
    spell_runtime: Any = None
    economy_runtime: Any = None
    npc_runtime: Any = None
