from __future__ import annotations
from dataclasses import dataclass, field
from heapq import heappush, heappop
from itertools import count
from typing import Any, Callable, Dict, List, Optional

_counter = count()

@dataclass(order=True)
class QueuedEvent:
    due_tick: int
    priority: int
    sequence: int = field(default_factory=lambda: next(_counter))
    event_type: str = field(compare=False, default="event")
    payload: Dict[str, Any] = field(compare=False, default_factory=dict)
    handler: Optional[Callable[[Any, Dict[str, Any]], None]] = field(compare=False, default=None)

class EventQueue:
    def __init__(self):
        self._heap: List[QueuedEvent] = []

    def schedule(self, due_tick: int, event_type: str, payload: Optional[Dict[str, Any]] = None, priority: int = 100, handler=None) -> QueuedEvent:
        event = QueuedEvent(due_tick=due_tick, priority=priority, event_type=event_type, payload=payload or {}, handler=handler)
        heappush(self._heap, event)
        return event

    def pop_due(self, current_tick: int) -> List[QueuedEvent]:
        due: List[QueuedEvent] = []
        while self._heap and self._heap[0].due_tick <= current_tick:
            due.append(heappop(self._heap))
        return due

    def __len__(self) -> int:
        return len(self._heap)
