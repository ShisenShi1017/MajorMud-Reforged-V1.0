#!/usr/bin/env python3
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
E=ROOT/"Librarium/Evidence/native_wcc_available_artifact_exhaustion_0034.json"
def main():
 d=json.loads(E.read_text(encoding="utf-8"))
 assert d["classification"]=="PROVEN"
 assert d["result"]=="AVAILABLE_WCC_ARTIFACTS_EXHAUSTED"
 assert all(d["wcc_exit_criteria"].values())
 assert len(d["decoded_or_closed_families"]) >= 9
 return 0
if __name__=="__main__": raise SystemExit(main())
