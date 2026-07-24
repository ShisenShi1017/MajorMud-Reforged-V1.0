#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Data/NativeDecodeReports/CodeUpdate0162CharacterSaveLoad/character_save_load_contract_0162.json"

def main() -> None:
    data = json.loads(OUT.read_text(encoding="utf-8"))
    assert data["authority"] == "Librarium"
    assert data["contract"]["serialization"]["round_trip_validation"] is True
    assert data["contract"]["load"]["backup_recovery"].startswith("only when")
    OUT.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

if __name__ == "__main__":
    main()
