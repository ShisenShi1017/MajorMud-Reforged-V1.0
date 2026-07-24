#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
contract=json.loads((ROOT/'Data/NativeDecodeReports/CodeUpdate0159CharacterInventory/character_inventory_gold_contract_0159.json').read_text())
assert contract['classification']=='STRONGLY CORRELATED'
assert contract['capacity_quarantine']['classification']=='UNKNOWN'
items=(ROOT/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
combat=(ROOT/'Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs').read_text()
trade=(ROOT/'Apps/Source/Reforged.Server/RuntimeLiveGameplayPart6.cs').read_text()
for token in ['public static class RuntimeCharacterGold','checked(current + amount)','checked(current - amount)','Gold conservation failed','EvidenceClassification = "STRONGLY CORRELATED"']:
    assert token in items, token
assert 'EnforceCapacity(' not in items
assert 'RuntimeCharacterGold.Credit(state.Gold, npc.GoldReward)' in combat
assert 'RuntimeCharacterGold.Transfer(sourceState.Gold, targetState.Gold, amount)' in trade
assert 'checked(sourceState.Gold - amount)' not in trade
print('character inventory 0159 validation passed')
