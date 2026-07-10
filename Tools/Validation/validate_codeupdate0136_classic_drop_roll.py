from __future__ import annotations

from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from Runtime.loot.loot_runtime import LootRuntime


def main() -> int:
    rule_path = ROOT / "Librarium" / "Objects" / "loot_rules" / "classic_drop_roll_rule_0136.json"
    rule = json.loads(rule_path.read_text(encoding="utf-8"))

    failures: list[str] = []
    for percent, expected in [(0, 0), (1, 1), (3, 3), (100, 100)]:
        actual = sum(1 for roll in range(1, 101) if LootRuntime.drop_roll_succeeds(percent, roll))
        if actual != expected:
            failures.append(f"percent {percent}: expected {expected} successful buckets, got {actual}")

    if rule.get("algorithm", {}).get("success_condition") != "roll <= drop_percent":
        failures.append("Librarium loot rule does not record roll <= drop_percent.")

    runtime = LootRuntime(seed=1)
    if not runtime.drop_roll_succeeds(1, 1):
        failures.append("1% drop must succeed on roll 1.")
    if runtime.drop_roll_succeeds(1, 2):
        failures.append("1% drop must fail on roll 2.")
    if not all(runtime.drop_roll_succeeds(3, roll) for roll in [1, 2, 3]):
        failures.append("3% drop must succeed on rolls 1, 2, and 3.")
    if runtime.drop_roll_succeeds(3, 4):
        failures.append("3% drop must fail on roll 4.")

    if failures:
        print("FAIL CodeUpdate0136 Classic drop roll validation")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print("PASS CodeUpdate0136 Classic drop roll validation")
    print("1% => 1/100 buckets; 3% => 3/100 buckets; roll contract: floor(random()*100)+1 semantics")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
