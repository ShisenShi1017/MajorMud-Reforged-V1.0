from __future__ import annotations
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from Runtime.test_harness import FullGameTestHarness, FullGameHarnessConfig

if __name__ == "__main__":
    harness = FullGameTestHarness(ROOT, FullGameHarnessConfig())
    result = harness.run()
    report = harness.write_report(result)
    print(json.dumps({"status": result.status, "passed": result.passed, "failed": result.failed, "unknown": result.unknown, "report": str(report)}, indent=2))
    raise SystemExit(1 if result.failed else 0)
