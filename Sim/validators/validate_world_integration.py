from __future__ import annotations

import json
from pathlib import Path


def validate_world_integration(repo_root: Path):
    from Runtime.world_integration import WorldRuntime, WorldRuntimeConfig

    notes = []
    passed = 0
    failed = 0
    unknown = 0

    runtime = WorldRuntime(repo_root, WorldRuntimeConfig(world_id="validation_runtime_0015", max_spawn_monsters=2, max_ground_items=2))
    world = runtime.load_or_create_world()

    if world.players:
        passed += 1
    else:
        failed += 1; notes.append("World integration did not create/load a player.")

    if world.monsters:
        passed += 1
    else:
        failed += 1; notes.append("World integration did not seed monsters.")

    result = runtime.run_offline_script(commands=["look", "get runtime", "attack goblin", "cast heal Runtime", "say validation", "who"], ticks=12)

    if result.commands_run == 6 and all(r.get("ok") for r in result.command_results if r.get("raw") in ("look", "say validation", "who")):
        passed += 1
    else:
        failed += 1; notes.append("Command script did not execute expected successful core commands.")

    if result.events_after > result.events_before:
        passed += 1
    else:
        failed += 1; notes.append("World ticks did not produce events.")

    if result.save_paths and all(Path(p).exists() for p in result.save_paths):
        passed += 1
    else:
        failed += 1; notes.append("Autosave did not create expected save files.")

    if runtime.summary().get("milestone") == "Runtime-0015":
        passed += 1
    else:
        failed += 1; notes.append("Runtime summary does not identify Runtime-0015.")

    report_dir = Path(repo_root) / "Reports"
    report_dir.mkdir(exist_ok=True)
    (report_dir / "world_integration_report_0015.json").write_text(json.dumps(result.to_dict(), indent=2, default=str), encoding="utf-8")

    return {
        "status": "PASS" if failed == 0 else "FAIL",
        "passed": passed,
        "failed": failed,
        "unknown": unknown,
        "notes": notes or ["World runtime integration loaded, processed commands, ticked systems, and saved runtime state."],
    }
