#!/usr/bin/env python3
import json,pathlib,sys,re
root=pathlib.Path(__file__).resolve().parents[2]; errs=[]
for n in ['items.json','equipment.json','equipment_slots.json','equipment_rules.json']:
 p=root/'Data/Classic'/n
 if not p.exists(): errs.append(f'missing {n}'); continue
 d=json.loads(p.read_text());
 if d.get('authority')!='Librarium': errs.append(f'{n} authority')
 if d.get('record_count')!=len(d.get('records',[])): errs.append(f'{n} count')
items=json.loads((root/'Data/Classic/items.json').read_text())['records']; eq=json.loads((root/'Data/Classic/equipment.json').read_text())['records']
ids=[x['id'] for x in items]
if len(ids)!=len(set(ids)): errs.append('duplicate canonical item ids')
if not any(x['slot']=='FACE' for x in eq): errs.append('FACE equipment missing')
code=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
for token in ['RuntimeItemInstance','RuntimeItemEngine','MoveToContainer','Equip(','Unequip(','Give(','Drop(','Use(','Duplicate item instance ID','Two-handed item conflicts']:
 if token not in code: errs.append('runtime token '+token)
print(json.dumps({'status':'PASS' if not errs else 'FAIL','items':len(items),'equipment':len(eq),'errors':errs},indent=2))
sys.exit(bool(errs))
