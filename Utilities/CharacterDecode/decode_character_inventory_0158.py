#!/usr/bin/env python3
import json, pathlib, hashlib
ROOT=pathlib.Path(__file__).resolve().parents[2]
items=json.loads((ROOT/'Data/Classic/items.json').read_text())['records']
rows=[]
for item in items:
    raw=item.get('economy',{}).get('weight')
    try: weight=int(raw)
    except Exception as exc: raise SystemExit(f"invalid weight {item.get('id')}: {raw!r}") from exc
    rows.append({'item_id':item['id'],'legacy_id':item.get('legacy_id'),'weight':weight,'classification':'PROVEN','source':'Data/Classic/items.json:economy.weight'})
contract={
 'code_update':'0158','subsystem':'character.inventory.weight','classification':'PROVEN',
 'authority':'Librarium generated Classic item projection','record_count':len(rows),
 'formula':'sum(item.economy.weight * stack_quantity) for all character/equipment/container instances owned by character',
 'location_scope':['character','equipment','container'],
 'excluded_locations':['room','corpse','npc','trade'],
 'statistics':{'minimum':min(x['weight'] for x in rows),'maximum':max(x['weight'] for x in rows),'zero_weight_count':sum(x['weight']==0 for x in rows),'sentinel_9999_count':sum(x['weight']==9999 for x in rows),'sentinel_32000_count':sum(x['weight']==32000 for x in rows)},
 'runtime_impact':['canonical unit weight loaded into RuntimeItemDefinition','stack weight uses checked 64-bit multiplication','owner carried weight uses checked 64-bit accumulation','nested containers count each owned contained instance exactly once'],
 'save_impact':'none; weight is definition-derived and reconstructed after load',
 'world_state_impact':'none until native encumbrance thresholds are recovered',
 'assumptions':['economy.weight is the canonical integer unit owned by MMExplorer/Librarium','container contents retain owner identity as established in CodeUpdate0157'],
 'fallback':'invalid, negative, or overflowing weights fail closed; no encumbrance restriction is guessed',
 'unknown':['carry capacity formula','encumbrance bands and penalties','container weight reduction semantics','coin weight','overflow destination behavior'],
 'records':rows}
out=ROOT/'Data/NativeDecodeReports/CodeUpdate0158CharacterInventory/character_inventory_weight_contract_0158.json'
out.write_text(json.dumps(contract,indent=2)+'\n')
print(out)
