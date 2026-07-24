#!/usr/bin/env python3
"""Emit the CodeUpdate0163 Character save/load lifecycle evidence contract."""
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
source = ROOT / "Data/NativeDecodeReports/CodeUpdate0163CharacterSaveLoad/character_save_load_contract_0163.json"
print(json.dumps(json.loads(source.read_text(encoding="utf-8")), indent=2, sort_keys=True))
