#!/usr/bin/env python3
import json, pathlib, hashlib
ROOT=pathlib.Path(__file__).resolve().parents[2]
src=ROOT/'Librarium/Objects/items/item_records_0122.json'
out=ROOT/'Data/Classic'
raw=json.loads(src.read_text(encoding='utf-8-sig'))
records=raw.get('records',raw if isinstance(raw,list) else [])
slots={0:'NONE',2:'HEAD',3:'FACE',4:'NECK',5:'BACK',6:'TORSO',7:'ARMS',8:'HANDS',9:'WAIST',10:'LEGS',11:'FEET',12:'WIELD',13:'OFF_HAND',14:'FINGER',15:'WRIST',16:'SHIELD',19:'LIGHT'}
slot_codes={v:k for k,v in slots.items()}
def i(v,d=0):
 try:return int(v)
 except:return d
fix_records=json.loads((out/'classic_bug_fixes.json').read_text(encoding='utf-8')).get('records',[])
item_slot_fixes={}
item_use_fixes={}
for fix in fix_records:
 if fix.get('id')=='classic_fix.mask_face_slot':
  fixed_slot=str(fix.get('slot','')).upper()
  for item_ref in fix.get('item_refs',[]):
   item_slot_fixes[item_ref]=fixed_slot
 if fix.get('id')=='classic_fix.lyrists_companion':
  item_use_fixes[fix.get('item_ref')]={'spell_ref':fix.get('spell_ref')}
items=[]; eq=[]
for r in records:
 e=r.get('equipment',{}); c=r.get('classification',{}); f=r.get('flags',{})
 worn=i(e.get('worn_on')); slot=slots.get(worn,'UNKNOWN')
 if r['id'] in item_slot_fixes:
  slot=item_slot_fixes[r['id']]
  worn=slot_codes.get(slot,worn)
 item={'id':r['id'],'legacy_id':str(r.get('legacy_id','')),'name':r.get('name',''),'item_type':c.get('type','misc'),'gettable':bool(f.get('gettable',False)),'container':c.get('type')=='container','consumable':c.get('type') in ('consumable','food','drink'),'stackable':False,'default_charges':i(r.get('charges',0)),'authority':'Librarium','source':str(src.relative_to(ROOT))}
 if r['id'] in item_slot_fixes:
  item['equipment']={'slot_name':slot.lower(),'slot':slot,'evidence':'classic_bug_fixes.json'}
 if r['id'] in item_use_fixes:
  item['use_effect']=item_use_fixes[r['id']]
 items.append(item)
 if worn:
  eq.append({'item_id':r['id'],'slot':slot,'worn_on':worn,'two_handed':bool(e.get('two_handed',False)),'class_restricted':bool(e.get('class_restricted',False)),'race_restricted':bool(e.get('race_restricted',False)),'minimum_level':i(e.get('level_requirement',0)),'minimum_strength':i(e.get('str_requirement',0)),'alignment':e.get('alignment'),'gender':e.get('gender'),'authority':'Librarium'})
base={'authority':'Librarium','compiler_code_update':'0137','generated_by':'compile_items_equipment_0137.py'}
for name,data in [('items.json',items),('equipment.json',eq),('equipment_slots.json',[{'code':k,'id':v} for k,v in sorted(slots.items())]),('equipment_rules.json',[{'id':'two-handed-shield-conflict','rule':'TWO_HANDED_EXCLUDES_OFF_HAND_AND_SHIELD'},{'id':'single-instance-single-location','rule':'ITEM_INSTANCE_HAS_EXACTLY_ONE_LOCATION'},{'id':'mask-face-slot','rule':'MASK_EQUIPS_FACE'}])]:
 payload=base|{'record_count':len(data),'records':data}
 (out/name).write_text(json.dumps(payload,indent=2)+"\n")
print(f'compiled {len(items)} items and {len(eq)} equipment records')
