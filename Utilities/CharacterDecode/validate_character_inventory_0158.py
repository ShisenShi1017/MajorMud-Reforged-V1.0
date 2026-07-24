#!/usr/bin/env python3
import json, pathlib
ROOT=pathlib.Path(__file__).resolve().parents[2]
contract=json.loads((ROOT/'Data/NativeDecodeReports/CodeUpdate0158CharacterInventory/character_inventory_weight_contract_0158.json').read_text())
items=json.loads((ROOT/'Data/Classic/items.json').read_text())['records']
assert contract['classification']=='PROVEN'
assert contract['record_count']==len(items)==1950
assert len(contract['records'])==len({x['item_id'] for x in contract['records']})
assert all(isinstance(x['weight'],int) and x['weight']>=0 for x in contract['records'])
source=(ROOT/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
for token in ['int Weight,','RuntimeInventoryWeightTotals','InventoryWeight(string ownerId)','checked((long)definition.Weight * x.StackQuantity)','IsCarriedLocation','WEIGHT_OVERFLOW']:
    assert token in source, token
assert 'capacity' not in contract['formula'].lower()
print('character inventory 0158 validation passed')
