from __future__ import annotations

import json
import os
import sys
import traceback
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from Sim.verification_registry import VALIDATORS

REPORT_DIR = ROOT / "Reports"
REPORT_DIR.mkdir(exist_ok=True)


def run_suite(repo_root: Path = ROOT) -> dict:
    results = {
        "suite": "MajorMUD Reforged Runtime Verification Suite",
        "milestone": "Runtime-0016",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "repo_root": str(repo_root),
        "results": [],
    }
    for validator in VALIDATORS:
        name = validator.__name__
        print(f"Running {name}...")
        try:
            result = validator(repo_root)
        except Exception as exc:
            result = {
                "status": "ERROR",
                "passed": 0,
                "failed": 1,
                "unknown": 0,
                "notes": [str(exc), traceback.format_exc()],
            }
        results["results"].append({"validator": name, **result})
    out_path = REPORT_DIR / "verification_suite_report.json"
    out_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"Report written to: {out_path}")
    return results

if __name__ == "__main__":
    run_suite()
