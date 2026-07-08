from __future__ import annotations
from dataclasses import dataclass, field
from typing import Dict, List
import shlex

DIRECTION_ALIASES = {"n":"north","s":"south","e":"east","w":"west","u":"up","d":"down","ne":"northeast","nw":"northwest","se":"southeast","sw":"southwest"}

@dataclass(frozen=True)
class ParsedCommand:
    raw: str
    verb: str
    args: List[str] = field(default_factory=list)
    arg_text: str = ""

class CommandParser:
    def __init__(self, aliases: Dict[str, str] | None = None):
        self.aliases = {**DIRECTION_ALIASES, **(aliases or {})}

    def parse(self, raw: str) -> ParsedCommand:
        raw = (raw or "").strip()
        if not raw:
            return ParsedCommand(raw=raw, verb="", args=[], arg_text="")
        try:
            parts = shlex.split(raw)
        except ValueError:
            parts = raw.split()
        if not parts:
            return ParsedCommand(raw=raw, verb="", args=[], arg_text="")
        verb = parts[0].lower()
        verb = self.aliases.get(verb, verb)
        args = parts[1:]
        return ParsedCommand(raw=raw, verb=verb, args=args, arg_text=" ".join(args))
