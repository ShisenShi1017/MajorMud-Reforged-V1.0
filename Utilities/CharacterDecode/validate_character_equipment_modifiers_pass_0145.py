#!/usr/bin/env python3
import json,sys
from pathlib import Path
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[2]
c=json.loads((root/"Data/NativeDecodeReports/CodeUpdate0145CharacterEquipment/character_equipment_modifier_contract_0145.json").read_text())
s=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
assert c["source_records"]["item_count"]==1950
for token in ["RuntimeEquipmentModifierTotals","EquipmentModifierTotals","ArmorClassBonus","DamageResistanceBonus","AccuracyBonus","SpeedBonus","checked"]: assert token in s, token
print("PASS character equipment modifiers 0145")
