#!/usr/bin/env python3
"""Run the complete CodeUpdate0135 plus internal 0136/0137 verification gate.

This orchestrates Python validation/simulation, .NET builds/tests, server smoke
modes, and a scripted local-console runtime session. It writes machine-readable
and Markdown reports under Reports/RuntimeVerification.
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

PYTHON_SCRIPTS = [
    "Librarium/Tools/validate_librarium.py",
    "Librarium/Validation/validate_librarium_0009.py",
    "Librarium/Validation/validate_librarium_0011.py",
    "Librarium/Validation/validate_validation_0001.py",
    "Runtime/test_harness/full_game_test_harness.py",
    "Sim/validators/validate_ability_quest_engine.py",
    "Sim/validators/validate_class_race_engine.py",
    "Sim/validators/validate_combat.py",
    "Sim/validators/validate_command_gameloop.py",
    "Sim/validators/validate_data_load.py",
    "Sim/validators/validate_exp_leveling.py",
    "Sim/validators/validate_formula_engine.py",
    "Sim/validators/validate_full_game_harness.py",
    "Sim/validators/validate_gameplay_completion.py",
    "Sim/validators/validate_loot_economy_npc.py",
    "Sim/validators/validate_monster_ai.py",
    "Sim/validators/validate_movement.py",
    "Sim/validators/validate_persistence.py",
    "Sim/validators/validate_reference_integrity.py",
    "Sim/validators/validate_regeneration.py",
    "Sim/validators/validate_runtime_placeholders.py",
    "Sim/validators/validate_schema_adapters.py",
    "Sim/validators/validate_snapshot_equipment.py",
    "Sim/validators/validate_spawn.py",
    "Sim/validators/validate_spells_status.py",
    "Sim/validators/validate_world_integration.py",
    "Tests/test_librarium_export_0116.py",
    "Tools/Librarium/validate_0127_world_outputs.py",
    "Tools/Librarium/validate_generation_readiness.py",
    "Tools/Librarium/validate_librarium_phase2_3.py",
    "Tools/Librarium/validate_librarium_population_0122.py",
    "Tools/Librarium/validate_librarium_population_0123.py",
    "Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/tests/smoke_test.py",
    "Tools/Validation/validate_codeupdate0130_server_classic_readiness.py",
    "Tools/Validation/validate_codeupdate0132_runtime_room_exit_slice.py",
    "Tools/Validation/validate_codeupdate0134_runtime_npc_loading.py",
    "Tools/Validation/validate_codeupdate0135_combat_execution.py",
    "Tools/Validation/validate_codeupdate0135_exhaustive_repairs.py",
    "Tools/Validation/validate_codeupdate0136_classic_drop_roll.py",
    "Tools/Validation/validate_codeupdate0137_character_foundation_librarium.py",
    "Tools/Validation/validate_codeupdate0137_character_foundation.py",
    "Tools/Validation/validate_codeupdate0137_character_foundation_phase003.py",
    "Tools/Validation/validate_codeupdate0137_items_equipment.py",
    "Tools/Validation/validate_codeupdate0137_item_runtime_complete.py",
    "Tools/Validation/validate_item_runtime_complete_0137.py",
    "Tools/Validation/validate_server_generated_data_bootstrap_0137.py",
    "Tools/Validation/validate_combat_runtime.py",
    "Tools/Validation/validate_combat_runtime_part2.py",
    "Tools/Validation/validate_gameplay_group1.py",
    "Tools/Validation/validate_gameplay_group2.py",
    "Tools/Validation/validate_gameplay_group3.py",
    "Tools/Validation/validate_gameplay_group4.py",
    "Sim/validators/validate_spell_runtime_0137.py",
    "Sim/validators/validate_spell_runtime_part2_0137.py",
]

DOTNET_STEPS = [
    ("dotnet_info", ["dotnet", "--info"]),
    ("solution_restore", ["dotnet", "restore", "MajorMud.Reforged.sln"]),
    ("solution_build", ["dotnet", "build", "MajorMud.Reforged.sln", "--configuration", "Release", "--no-restore"]),
    ("compiler_tests_build", ["dotnet", "build", "Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj", "--configuration", "Release", "--no-restore"]),
    ("compiler_tests_run", ["dotnet", "test", "Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj", "--configuration", "Release", "--no-build"]),
    ("core_tests_build", ["dotnet", "build", "Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj", "--configuration", "Release", "--no-restore"]),
    ("core_tests_run", ["dotnet", "run", "--project", "Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj", "--configuration", "Release", "--no-build"]),
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
]

LOCAL_COMMANDS = "help\nlook\nwhere\nworld\nworlds\nsessions\nstate\nevents\nnorth\nlook\nsouth\nquit\n"


def run_step(root: Path, name: str, command: list[str], *, stdin: str | None = None, timeout: int = 600) -> dict:
    started = time.monotonic()
    env = os.environ.copy()
    env["PYTHONPATH"] = str(root) + os.pathsep + env.get("PYTHONPATH", "")
    try:
        proc = subprocess.run(
            command,
            cwd=root,
            input=stdin,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=timeout,
            env=env,
        )
        return {
            "name": name,
            "command": command,
            "status": "PASS" if proc.returncode == 0 else "FAIL",
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
    except OSError as exc:
        return {
            "name": name,
            "command": command,
            "status": "ERROR",
            "exit_code": None,
            "seconds": round(time.monotonic() - started, 3),
            "output": str(exc),
        }


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    report_dir = root / "Reports" / "RuntimeVerification"
    report_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    results: list[dict] = []

    if shutil.which("dotnet") is None:
        print("FAIL: dotnet SDK was not found on PATH.", file=sys.stderr)
        return 2

    print(f"Repository: {root}")
    print("Running complete CodeUpdate0135 plus internal 0136/0137 runtime verification gate...")

    for script in PYTHON_SCRIPTS:
        path = root / script
        if not path.is_file():
            results.append({"name": script, "command": [sys.executable, script], "status": "FAIL", "exit_code": None, "seconds": 0, "output": "Script missing"})
            continue
        result = run_step(root, f"python:{script}", [sys.executable, script])
        results.append(result)
        print(f"{result['status']:7} {result['name']} ({result['seconds']}s)")
        if result["status"] != "PASS":
            break

    if all(r["status"] == "PASS" for r in results):
        for name, command in DOTNET_STEPS:
            result = run_step(root, name, command, timeout=1200)
            results.append(result)
            print(f"{result['status']:7} {name} ({result['seconds']}s)")
            if result["status"] != "PASS":
                break

    if all(r["status"] == "PASS" for r in results):
        result = run_step(
            root,
            "server_scripted_local_session",
            ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--run-local"],
            stdin=LOCAL_COMMANDS,
            timeout=180,
        )
        required_markers = ["Local console session", "Commands:", "Exits:"]
        missing = [marker for marker in required_markers if marker not in result["output"]]
        if result["status"] == "PASS" and missing:
            result["status"] = "FAIL"
            result["exit_code"] = 90
            result["output"] += "\nMissing expected runtime markers: " + ", ".join(missing)
        results.append(result)
        print(f"{result['status']:7} {result['name']} ({result['seconds']}s)")

    summary = {
        "codeupdate": "0137",
        "gate": "complete_runtime_verification",
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "status": "PASS" if results and all(r["status"] == "PASS" for r in results) else "FAIL",
        "total": len(results),
        "passed": sum(r["status"] == "PASS" for r in results),
        "failed": sum(r["status"] != "PASS" for r in results),
        "results": results,
    }
    json_path = report_dir / f"complete_runtime_verification_{stamp}.json"
    md_path = report_dir / f"complete_runtime_verification_{stamp}.md"
    json_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    lines = [
        "# Complete Runtime Verification",
        "",
        f"- CodeUpdate: 0137",
        f"- Status: **{summary['status']}**",
        f"- Passed: {summary['passed']}/{summary['total']}",
        f"- Timestamp: {summary['timestamp_utc']}",
        "",
        "| Status | Step | Seconds |",
        "|---|---|---:|",
    ]
    for result in results:
        lines.append(f"| {result['status']} | `{result['name']}` | {result['seconds']} |")
    lines += ["", f"Raw details: `{json_path.relative_to(root)}`", ""]
    md_path.write_text("\n".join(lines), encoding="utf-8")

    print(f"Report: {md_path.relative_to(root)}")
    print(f"Raw:    {json_path.relative_to(root)}")
    return 0 if summary["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
