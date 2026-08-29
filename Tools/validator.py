#!/usr/bin/env python3
"""Single current-health validator for MajorMUD Reforged.

One-time import, decode, and promotion checks are recorded in
ValidationManifest.json after they pass. This command only validates the current
accepted baseline still compiles, loads, and runs the core smoke surfaces.
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path


PYTHON_STEPS = [
    ("librarium_catalog", ["Librarium/Tools/validate_librarium.py"]),
    ("full_game_harness", ["Runtime/test_harness/full_game_test_harness.py"]),
    ("ability_quest_engine", ["Sim/validators/validate_ability_quest_engine.py"]),
    ("class_race_engine", ["Sim/validators/validate_class_race_engine.py"]),
    ("combat", ["Sim/validators/validate_combat.py"]),
    ("command_gameloop", ["Sim/validators/validate_command_gameloop.py"]),
    ("data_load", ["Sim/validators/validate_data_load.py"]),
    ("experience_leveling", ["Sim/validators/validate_exp_leveling.py"]),
    ("formula_engine", ["Sim/validators/validate_formula_engine.py"]),
    ("gameplay_completion", ["Sim/validators/validate_gameplay_completion.py"]),
    ("loot_economy_npc", ["Sim/validators/validate_loot_economy_npc.py"]),
    ("monster_ai", ["Sim/validators/validate_monster_ai.py"]),
    ("movement", ["Sim/validators/validate_movement.py"]),
    ("persistence", ["Sim/validators/validate_persistence.py"]),
    ("reference_integrity", ["Sim/validators/validate_reference_integrity.py"]),
    ("regeneration", ["Sim/validators/validate_regeneration.py"]),
    ("runtime_placeholders", ["Sim/validators/validate_runtime_placeholders.py"]),
    ("schema_adapters", ["Sim/validators/validate_schema_adapters.py"]),
    ("snapshot_equipment", ["Sim/validators/validate_snapshot_equipment.py"]),
    ("spawn", ["Sim/validators/validate_spawn.py"]),
    ("spells_status", ["Sim/validators/validate_spells_status.py"]),
    ("world_integration", ["Sim/validators/validate_world_integration.py"]),
    ("generation_readiness", ["Tools/Librarium/validate_generation_readiness.py"]),
    ("remaining_systems", ["Tools/Validation/validate_remaining_systems.py"]),
    ("combat_runtime", ["Tools/Validation/validate_combat_runtime.py"]),
    ("combat_runtime_part2", ["Tools/Validation/validate_combat_runtime_part2.py"]),
    ("gameplay_group1", ["Tools/Validation/validate_gameplay_group1.py"]),
    ("gameplay_group2", ["Tools/Validation/validate_gameplay_group2.py"]),
    ("gameplay_group3", ["Tools/Validation/validate_gameplay_group3.py"]),
    ("gameplay_group4", ["Tools/Validation/validate_gameplay_group4.py"]),
    ("live_gameplay_part5", ["Tools/Validation/validate_live_gameplay_part5.py"]),
]

DOTNET_STEPS = [
    ("dotnet_info", ["dotnet", "--info"]),
    ("solution_restore", ["dotnet", "restore", "MajorMud.Reforged.sln"]),
    ("solution_build", ["dotnet", "build", "MajorMud.Reforged.sln", "--configuration", "Release", "--no-restore"]),
    ("classic_generation", ["dotnet", "run", "--project", "Apps/Compiler/MajorMud.Reforged.Compiler.csproj", "--configuration", "Release", "--no-build", "--", "build", "--world", "Classic", "--clean"]),
    ("compiler_tests", ["dotnet", "test", "Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj", "--configuration", "Release", "--no-build"]),
    ("core_tests_restore", ["dotnet", "restore", "Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj"]),
    ("core_tests_build", ["dotnet", "build", "Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj", "--configuration", "Release", "--no-restore"]),
    ("core_smoke", ["dotnet", "run", "--project", "Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj", "--configuration", "Release", "--no-build"]),
    ("classic_data_audit", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--audit-classic-data"]),
    ("server_generated_data", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-generated-data"]),
    ("server_room_exits", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-room-exits"]),
    ("server_npcs", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-npcs"]),
    ("server_character_foundation", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-character-foundation"]),
    ("server_items", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-items"]),
    ("server_spells", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-spells"]),
    ("server_spell_execution", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-spell-execution"]),
    ("server_combat", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-combat"]),
    ("server_combat_runtime", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-combat-runtime"]),
    ("server_combat_runtime_part2", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-combat-runtime-part2"]),
    ("server_actors_abilities", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-actors-abilities"]),
    ("server_interaction_narrative", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-interaction-narrative"]),
    ("server_services_economy", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-services-economy"]),
    ("server_world_multiplayer", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-world-multiplayer"]),
    ("server_live_gameplay", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-live-gameplay"]),
    ("classic_parity_closure", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-classic-parity-closure"]),
]


def run_step(root: Path, name: str, command: list[str], timeout: int = 1200) -> dict:
    started = time.monotonic()
    env = os.environ.copy()
    env["PYTHONPATH"] = str(root) + os.pathsep + env.get("PYTHONPATH", "")
    try:
        proc = subprocess.run(
            command,
            cwd=root,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=timeout,
            env=env,
        )
        status = "PASS" if proc.returncode == 0 else "FAIL"
        return {
            "name": name,
            "command": command,
            "status": status,
            "exit_code": proc.returncode,
            "seconds": round(time.monotonic() - started, 3),
            "output": proc.stdout,
        }
    except subprocess.TimeoutExpired as exc:
        return {
            "name": name,
            "command": command,
            "status": "TIMEOUT",
            "exit_code": None,
            "seconds": round(time.monotonic() - started, 3),
            "output": (exc.stdout or "") + "\nTIMEOUT",
        }


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    if shutil.which("dotnet") is None:
        print("FAIL: dotnet SDK was not found on PATH.", file=sys.stderr)
        return 2

    report_dir = root / "Reports" / "RuntimeVerification"
    report_dir.mkdir(parents=True, exist_ok=True)
    results: list[dict] = []

    print(f"Repository: {root}")
    print("Running Reforged current-health validator...")

    for name, args in PYTHON_STEPS:
        script = root / args[0]
        if not script.is_file():
            results.append({"name": name, "command": [sys.executable, args[0]], "status": "FAIL", "exit_code": None, "seconds": 0, "output": "Script missing"})
            break
        result = run_step(root, name, [sys.executable, args[0]])
        results.append(result)
        print(f"{result['status']:7} {name} ({result['seconds']}s)")
        if result["status"] != "PASS":
            break

    if all(result["status"] == "PASS" for result in results):
        for name, command in DOTNET_STEPS:
            result = run_step(root, name, command)
            results.append(result)
            print(f"{result['status']:7} {name} ({result['seconds']}s)")
            if result["status"] != "PASS":
                break

    summary = {
        "gate": "current_health_validator",
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "status": "PASS" if results and all(result["status"] == "PASS" for result in results) else "FAIL",
        "passed": sum(result["status"] == "PASS" for result in results),
        "total": len(results),
        "results": results,
    }
    json_path = report_dir / "validator_latest.json"
    md_path = report_dir / "validator_latest.md"
    json_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    md_lines = [
        "# Reforged Validator",
        "",
        f"- Status: **{summary['status']}**",
        f"- Passed: {summary['passed']}/{summary['total']}",
        f"- Timestamp: {summary['timestamp_utc']}",
        "",
        "| Status | Step | Seconds |",
        "|---|---|---:|",
    ]
    for result in results:
        md_lines.append(f"| {result['status']} | `{result['name']}` | {result['seconds']} |")
    md_lines += ["", f"Raw details: `{json_path.relative_to(root)}`", ""]
    md_path.write_text("\n".join(md_lines), encoding="utf-8")

    print(f"Report: {md_path.relative_to(root)}")
    print(f"Raw:    {json_path.relative_to(root)}")
    return 0 if summary["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
