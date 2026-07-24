#!/usr/bin/env python3
from pathlib import Path
import hashlib, json

ROOT = Path(__file__).resolve().parents[2]
E = ROOT / "Librarium/Evidence/native_wcc_knows_gate_0032.json"

def main() -> int:
    d=json.loads(E.read_text(encoding="utf-8"))
    assert d["classification"] == "PROVEN"
    assert len(d["modules"]) == 2
    assert d["native_contract"]["mode_argument"] == 0
    assert d["native_contract"]["only_static_caller_va"] == "0x0040D661"
    assert any("No file contents" in x for x in d["limits"])
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
