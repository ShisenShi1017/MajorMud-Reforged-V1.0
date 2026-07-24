#!/usr/bin/env python3
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
persistence = (ROOT / "Apps/Source/Reforged.Server/RuntimeCharacterPersistence.cs").read_text(encoding="utf-8")
foundation = (ROOT / "Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs").read_text(encoding="utf-8")
host = (ROOT / "Apps/Source/Reforged.Server/ServerRuntimeHost.cs").read_text(encoding="utf-8")
contract = json.loads((ROOT / "Data/NativeDecodeReports/CodeUpdate0163CharacterSaveLoad/character_save_load_contract_0163.json").read_text())
checks = {
 "contract-version": 'ContractVersion = "0163"' in persistence,
 "autosave-due": "IsAutosaveDue" in persistence,
 "autosave-marker": "AutosaveMarker" in persistence,
 "startup-sweep": "StartupRecoverySweep" in foundation,
 "stable-order": "OrderBy(x => x.Actor.Id" in foundation,
 "scheduler-owner": '"character-autosave"' in host,
 "five-minute-policy": "TimeSpan.FromMinutes(5)" in host,
 "classification": contract["classification"] == "STRONGLY_CORRELATED"
}
failed=[k for k,v in checks.items() if not v]
if failed: raise SystemExit("failed: " + ", ".join(failed))
print("Character Save/Load 0163 validation passed")
