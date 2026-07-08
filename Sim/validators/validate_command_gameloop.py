from __future__ import annotations

from pathlib import Path


def validate_command_gameloop(repo_root: Path) -> dict:
    notes = []
    passed = 0
    failed = 0
    unknown = 0
    try:
        from Runtime.game_loop import RuntimeKernel
        from Runtime.command import CommandParser, CommandRegistry, CommandDispatcher, register_core_commands
    except Exception as exc:
        return {"status": "FAIL", "passed": 0, "failed": 1, "unknown": 0, "notes": [f"Import failed: {exc}"]}

    parser = CommandParser()
    parsed = parser.parse('cast heal "Runtime Tester"')
    if parsed.verb == "cast" and parsed.args == ["heal", "Runtime Tester"]:
        passed += 1
    else:
        failed += 1; notes.append(f"Parser quoted arg failed: {parsed}")

    registry = register_core_commands(CommandRegistry())
    required = {"look", "move", "inventory", "equipment", "get", "drop", "attack", "cast", "say", "whisper", "who", "help", "buy", "deposit"}
    names = set(registry.command_names())
    missing = required - names
    if not missing:
        passed += 1
    else:
        failed += 1; notes.append(f"Missing registered commands: {sorted(missing)}")

    try:
        kernel = RuntimeKernel(repo_root=repo_root).create_demo_world()
        actor_id = "player_runtime_tester"
        results = [
            kernel.submit_command(actor_id, "look"),
            kernel.submit_command(actor_id, "inventory"),
            kernel.submit_command(actor_id, "get runtime coin"),
            kernel.submit_command(actor_id, "drop runtime coin"),
            kernel.submit_command(actor_id, "say hello world"),
            kernel.submit_command(actor_id, "attack goblin"),
            kernel.submit_command(actor_id, "cast heal Runtime Tester"),
            kernel.submit_command(actor_id, "who"),
            kernel.submit_command(actor_id, "help"),
        ]
        if all(r.ok for r in results):
            passed += 1
        else:
            failed += 1; notes.append("One or more core demo commands failed: " + "; ".join(r.message for r in results if not r.ok))
        before_tick = kernel.world.tick
        ran = kernel.tick(12)
        summary = kernel.summary()
        if summary["tick"] == before_tick + 12 and summary["tasks"].get("event_queue", 0) >= 1 and summary["tasks"].get("cleanup", 0) >= 1:
            passed += 1
        else:
            failed += 1; notes.append(f"Scheduler did not run expected tasks: {summary}")
        if summary["events"] > 0 and len(kernel.command_history) >= 9:
            passed += 1
        else:
            failed += 1; notes.append(f"Runtime integration events/history insufficient: {summary}")
        notes.append(f"Kernel summary: {summary}")
    except Exception as exc:
        failed += 1
        notes.append(f"Kernel integration failed: {exc}")

    notes.append("Runtime-0012 command/game-loop uses Reforged formula provenance; classic command timing and parser quirks are not claimed verified.")
    status = "PASS" if failed == 0 else "FAIL"
    return {"status": status, "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
