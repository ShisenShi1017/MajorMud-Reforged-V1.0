from __future__ import annotations
from Runtime.command.command_result import CommandResult

class CommandDispatcher:
    def __init__(self, registry, parser):
        self.registry = registry
        self.parser = parser

    def dispatch(self, ctx, raw: str) -> CommandResult:
        parsed = self.parser.parse(raw)
        if not parsed.verb:
            return CommandResult.fail("No command entered.")
        command = self.registry.resolve(parsed.verb)
        if command is None:
            return CommandResult.fail(f"Unknown or ambiguous command: {parsed.verb}")
        try:
            result = command.execute(ctx, parsed)
        except Exception as exc:
            return CommandResult.fail(f"Command failed: {exc}", error=repr(exc))
        if result.message:
            ctx.world.add_event("command_result", ctx.actor_id, message=result.message, data={"command": parsed.raw, "ok": result.ok})
        return result
