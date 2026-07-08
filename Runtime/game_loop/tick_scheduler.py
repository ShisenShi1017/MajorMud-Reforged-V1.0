from __future__ import annotations
from dataclasses import dataclass, field
from typing import Callable, Dict, List

@dataclass
class TickTask:
    name: str
    interval_ticks: int
    priority: int
    callback: Callable[[object], None]
    next_tick: int = 0
    runs: int = 0

class TickScheduler:
    def __init__(self):
        self.tasks: List[TickTask] = []

    def add_task(self, name: str, interval_ticks: int, priority: int, callback: Callable[[object], None]) -> TickTask:
        task = TickTask(name=name, interval_ticks=max(1, interval_ticks), priority=priority, callback=callback)
        self.tasks.append(task)
        self.tasks.sort(key=lambda t: (t.priority, t.name))
        return task

    def run_due(self, kernel) -> List[str]:
        ran: List[str] = []
        current = kernel.world.tick
        for task in self.tasks:
            if current >= task.next_tick:
                task.callback(kernel)
                task.runs += 1
                task.next_tick = current + task.interval_ticks
                ran.append(task.name)
        return ran

    def summary(self) -> Dict[str, int]:
        return {task.name: task.runs for task in self.tasks}
