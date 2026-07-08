from __future__ import annotations
from pathlib import Path


def validate_full_game_harness(repo_root: Path):
    from Runtime.test_harness import FullGameTestHarness, FullGameHarnessConfig

    harness = FullGameTestHarness(repo_root, FullGameHarnessConfig(world_id="validation_runtime_0016_full_game", ticks=20))
    result = harness.run()
    report_path = harness.write_report(result, Path(repo_root) / "Reports" / "full_game_test_harness_validation_0016.json")

    notes = [
        f"Full game harness status: {result.status}.",
        f"Report written: {report_path}.",
        "UNKNOWN counts are expected where exact classic formulas/data remain evidence-gated.",
    ]
    return {
        "status": "PASS" if result.failed == 0 else "FAIL",
        "passed": result.passed,
        "failed": result.failed,
        "unknown": result.unknown,
        "notes": notes,
    }
