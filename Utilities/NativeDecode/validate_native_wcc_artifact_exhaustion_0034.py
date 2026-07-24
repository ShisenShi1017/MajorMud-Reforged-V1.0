#!/usr/bin/env python3
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
E=ROOT/"Librarium/Evidence/native_wcc_available_artifact_exhaustion_0034.json"
REQUIRED={"DAT","DLL/EXE","MCV/MSG/H/MDF","ADDON.SYS","DMA.SYS","KNOWS.SYS","BIN","HSE/STO"}
def main():
 d=json.loads(E.read_text(encoding="utf-8"))
 fam={x["family"] for x in d["decoded_or_closed_families"]}
 assert REQUIRED.issubset(fam)
 assert d["wcc_exit_criteria"]["further_wcc_only_static_pass_expected_to_add_semantics"] is False
 assert "matching WGSERVER.EXE" in d["next_required_artifacts"]
 return 0
if __name__=="__main__": raise SystemExit(main())
