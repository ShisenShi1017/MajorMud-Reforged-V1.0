from __future__ import annotations
from dataclasses import dataclass, field
from typing import Any, Dict, List

@dataclass
class CommandResult:
    ok: bool
    message: str = ""
    events: List[Dict[str, Any]] = field(default_factory=list)
    data: Dict[str, Any] = field(default_factory=dict)

    @classmethod
    def success(cls, message: str = "", **data: Any) -> "CommandResult":
        return cls(True, message, data=data)

    @classmethod
    def fail(cls, message: str = "", **data: Any) -> "CommandResult":
        return cls(False, message, data=data)
