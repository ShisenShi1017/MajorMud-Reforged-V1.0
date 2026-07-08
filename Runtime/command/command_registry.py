from __future__ import annotations
from typing import Dict, Iterable, Optional

class Command:
    name = ""
    aliases: Iterable[str] = ()
    help = ""
    def execute(self, ctx, parsed):
        raise NotImplementedError

class CommandRegistry:
    def __init__(self):
        self._commands: Dict[str, Command] = {}

    def register(self, command: Command) -> Command:
        names = [command.name, *list(command.aliases)]
        for name in names:
            if name:
                self._commands[name.lower()] = command
        return command

    def resolve(self, verb: str) -> Optional[Command]:
        verb = (verb or "").lower()
        if verb in self._commands:
            return self._commands[verb]
        matches = [cmd for name, cmd in self._commands.items() if name.startswith(verb)] if verb else []
        return matches[0] if len(set(id(m) for m in matches)) == 1 else None

    def command_names(self):
        return sorted(set(cmd.name for cmd in self._commands.values()))
