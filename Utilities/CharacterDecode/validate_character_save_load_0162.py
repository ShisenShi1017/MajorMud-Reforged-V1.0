#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Apps/Source/Reforged.Server/RuntimeCharacterPersistence.cs"
FOUNDATION = ROOT / "Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs"
CONTRACT = ROOT / "Data/NativeDecodeReports/CodeUpdate0162CharacterSaveLoad/character_save_load_contract_0162.json"
REPORT = ROOT / "Librarium/Reports/character_semantic_exhaustion_status_0162.json"


def require(value: bool, message: str) -> None:
    if not value:
        raise SystemExit(message)


def main() -> None:
    runtime = RUNTIME.read_text(encoding="utf-8")
    foundation = FOUNDATION.read_text(encoding="utf-8")
    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    report = json.loads(REPORT.read_text(encoding="utf-8"))
    for marker in ("ContractMarker", "SaveReasonMarker", "SaveSequenceMarker", "RecoveryMarker"):
        require(marker in runtime, f"missing persistence marker {marker}")
    for behavior in ("PrepareForSave", "ValidateRoundTrip", "ReadAndValidate", '"backup"'):
        require(behavior in runtime, f"missing persistence behavior {behavior}")
    require("retainBackup: true" in foundation, "character backup retention is not wired")
    require('"disconnect"' in foundation, "disconnect save reason is not wired")
    require(contract["contract"]["load"]["both_invalid"] == "fail_closed", "invalid-load fallback is not fail closed")
    require(report["code_update"] == "0162", "wrong report version")
    print("Character Save/Load 0162 validation passed")

if __name__ == "__main__":
    main()
