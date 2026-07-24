#!/usr/bin/env python3
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
contract = root / "Data/NativeDecodeReports/CodeUpdate0148CharacterFlags/character_flag_lifecycle_contract_0148.json"
semantic = root / "Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs"
resolver = root / "Apps/Source/Reforged.Server/RuntimeCharacterFlags.cs"
combat = root / "Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs"
live = root / "Apps/Source/Reforged.Server/RuntimeLiveWorld.cs"
for path in (contract, semantic, resolver, combat, live):
    if not path.is_file():
        raise SystemExit(f"missing:{path.relative_to(root)}")
data = json.loads(contract.read_text(encoding="utf-8"))
flags = {entry["id"]: entry for entry in data["flags"]}
assert set(flags) == {"dead", "resting"}
assert flags["dead"]["set_event"].startswith("player defeat")
assert flags["dead"]["clear_event"].startswith("respawn")
assert data["storage"]["save_version"] == 4
semantic_text = semantic.read_text(encoding="utf-8")
resolver_text = resolver.read_text(encoding="utf-8")
combat_text = combat.read_text(encoding="utf-8")
live_text = live.read_text(encoding="utf-8")
assert "CurrentSaveVersion = 4" in semantic_text
assert "ValidatePromotedFlagConsistency" in semantic_text
assert "RuntimeCharacterFlagIds.Dead" in resolver_text
assert "RuntimeCharacterFlagIds.Resting" in resolver_text
assert "RuntimeCharacterFlagResolver.Set" in combat_text
assert "RuntimeCharacterFlagResolver.Clear" in combat_text
assert "RuntimeCharacterFlagResolver.IsSet" in live_text
assert '["dead"] = "true"' not in combat_text
print("character flags 0148 validation passed")
