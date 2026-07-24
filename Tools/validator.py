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
    ("generation_readiness", ["Tools/Librarium/validate_generation_readiness.py"]),
]

DOTNET_STEPS = [
    ("dotnet_info", ["dotnet", "--info"]),
    ("solution_restore", ["dotnet", "restore", "MajorMud.Reforged.sln"]),
    ("solution_build", ["dotnet", "build", "MajorMud.Reforged.sln", "--configuration", "Release", "--no-restore"]),
    ("compiler_tests", ["dotnet", "test", "Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj", "--configuration", "Release", "--no-build"]),
    ("core_smoke", ["dotnet", "run", "--project", "Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj", "--configuration", "Release", "--no-build"]),
    ("classic_data_audit", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--audit-classic-data"]),
    ("server_generated_data", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-generated-data"]),
    ("server_room_exits", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-room-exits"]),
    ("server_npcs", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-npcs"]),
    ("server_interaction_narrative", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-interaction-narrative"]),
    ("server_live_gameplay", ["dotnet", "run", "--project", "Apps/Source/Reforged.Server", "--configuration", "Release", "--", "--smoke-live-gameplay"]),
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
