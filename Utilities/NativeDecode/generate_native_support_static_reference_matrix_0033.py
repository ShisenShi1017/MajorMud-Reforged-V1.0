#!/usr/bin/env python3
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
E=ROOT/"Librarium/Evidence/native_support_static_reference_matrix_0033.json"
def main():
 d=json.loads(E.read_text(encoding="utf-8")); assert d["classification"]=="PROVEN"; assert len(d["modules"])==6; assert d["negative_findings"]["hse_exact_filename_literals_in_supplied_native_modules"]==0; return 0
if __name__=="__main__": raise SystemExit(main())
