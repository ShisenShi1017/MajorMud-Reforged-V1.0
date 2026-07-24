#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
source = ROOT / "Librarium/RemainingSystems/remaining_systems_registry.json"
out = ROOT / "Reports/RemainingSystems/remaining_systems_registry.generated.md"
data = json.loads(source.read_text(encoding="utf-8"))
lines = ["# Remaining Systems Registry", "", f"Pass: `{data['updatedByPass']}`", "", "| Subsystem | Classification | Status |", "|---|---|---|"]
for entry in data["systems"]:
    lines.append(f"| `{entry['id']}` | {entry['classification']} | {entry['status']} |")
out.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(out.relative_to(ROOT))
