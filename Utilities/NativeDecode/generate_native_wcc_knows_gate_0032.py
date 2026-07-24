#!/usr/bin/env python3
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "Librarium/Evidence/native_wcc_knows_gate_0032.json"

def main() -> int:
    data = json.loads(EVIDENCE.read_text(encoding="utf-8"))
    assert data["evidence_id"] == "native_wcc_knows_gate_0032"
    assert data["native_contract"]["static_callsite_count_per_module"] == 1
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
