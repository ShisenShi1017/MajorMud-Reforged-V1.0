#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Librarium/Objects/character_foundation/native_runtime_promoted_0137.json"
OUTPUT = ROOT / "Data/NativeDecodeReports/CodeUpdate0143CharacterAlignment/character_alignment_contract_0143.json"

def main() -> None:
    source = json.loads(SOURCE.read_text(encoding="utf-8"))
    rules = source["character_creation_rules"]["alignment_evil_point_rules"]
    contract = json.loads(OUTPUT.read_text(encoding="utf-8"))
    assert rules["initial_evil_points"] == contract["initial_state"]["evil_points"]
    assert rules["initial_reputation"] == contract["initial_state"]["reputation"]
    assert rules["attack_reputation_penalties"] == contract["attack_reputation_penalties"]
    assert rules["fiend_attack_lockout"]["threshold"] == contract["fiend_attack_lockout"]["threshold"]
    print("character alignment contract 0143 verified")

if __name__ == "__main__":
    main()
