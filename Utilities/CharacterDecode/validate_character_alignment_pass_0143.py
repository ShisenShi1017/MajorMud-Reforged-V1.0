#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTRACT = ROOT / "Data/NativeDecodeReports/CodeUpdate0143CharacterAlignment/character_alignment_contract_0143.json"
CODE = ROOT / "Apps/Source/Reforged.Server/RuntimeCharacterAlignment.cs"
SEMANTIC = ROOT / "Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs"

def main() -> None:
    data = json.loads(CONTRACT.read_text(encoding="utf-8"))
    assert data["initial_state"] == {"evil_points": 0, "reputation": "Neutral"}
    assert data["attack_reputation_penalties"]["Saint"] == 2
    assert data["attack_reputation_penalties"]["Good"] == 2
    assert all(data["attack_reputation_penalties"][x] == 0 for x in ("Neutral", "Seedy", "Outlaw", "Criminal", "Villain", "Fiend"))
    assert data["fiend_attack_lockout"]["threshold"] == 300
    code = CODE.read_text(encoding="utf-8")
    assert "FiendAttackLockoutThreshold = 300" in code
    assert '"saint" or "good" => 2' in code
    assert "ApplyDailyCleanup" in code and "CycleMinutes" in code
    semantic = SEMANTIC.read_text(encoding="utf-8")
    assert "CurrentSaveVersion = 3" in semantic
    assert "RuntimeCharacterAlignmentState? Alignment" in semantic
    print("character alignment pass 0143 validation passed")

if __name__ == "__main__":
    main()
