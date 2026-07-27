#!/usr/bin/env python3
import csv, json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
items_path=root/'Data/Classic/items.json'
item_doc=json.loads(items_path.read_text(encoding='utf-8-sig'))
item_ids={str(r.get('id')) for r in item_doc.get('records',[]) if r.get('id')}

# Authoritative evidence matrix contains only actual stock relationships; null WCC slots are
# fixed-width placeholders/service controls and must never become item.unknown runtime stock.
matrix_path=root/'Evidence/DataSimVer0014/ShopInventoryEconomyEvidence.csv'
matrix={}
if matrix_path.exists():
    with matrix_path.open(encoding='utf-8-sig',newline='') as f:
        for row in csv.DictReader(f):
            sid=str(row.get('shop_id') or row.get('shop_number') or row.get('shop') or '').strip()
            iid=str(row.get('item_id') or row.get('item_number') or '').strip()
            if sid and iid and iid.lower() not in ('none','null'):
                try: iid=str(int(float(iid)))
                except ValueError: continue
                matrix.setdefault(sid,[]).append(iid)

paths=[root/'Librarium/Objects/shops/shop_records_0122.json',root/'Data/Classic/shops.json']
for p in paths:
    if not p.exists(): continue
    doc=json.loads(p.read_text(encoding='utf-8-sig'))
    records=doc.get('records', doc if isinstance(doc,list) else [])
    promoted=empty_removed=controls=0
    for r in records:
        fields=((r.get('public_reference_0139') or {}).get('fields') or {})
        if 'markup_percent' in fields:
            r['markup_percent']=int(fields['markup_percent']); promoted+=1
        if 'shop_type' in fields: r['decoded_shop_type']=fields['shop_type']
        if 'room_number' in fields and fields.get('room_number') is not None and not r.get('room_ref'):
            r['room_ref']=f"room.{fields['room_number']}"

        source_inventory=fields.get('inventory') if isinstance(fields.get('inventory'),list) else []
        cleaned=[]; non_inventory=[]
        for index,e in enumerate(source_inventory):
            iid=e.get('item_id')
            numeric={k:int(e.get(k,0) or 0) for k in ('max_qty','normal_qty','regen_qty','regen_time','regen_percent')}
            if iid is not None:
                ref=f'item.{int(iid)}'
                if ref not in item_ids:
                    raise RuntimeError(f"{r.get('id')} slot {index}: decoded item {ref} absent from items.json")
                cleaned.append({'item_ref':ref,'item_exists':True,**{k:str(v) for k,v in numeric.items()}})
                continue
            if all(v==0 for v in numeric.values()):
                empty_removed+=1
                continue
            # These are fixed-width non-item values. Preserve their decoded bytes/values as
            # service controls, never as purchasable inventory and never as unresolved links.
            service_type=int(fields.get('shop_type',r.get('service_type',0)) or 0)
            if service_type in (6,11): kind='service_control_slot'
            else: kind='unused_fixed_inventory_slot'
            non_inventory.append({'source_slot':index,'classification':kind,'decoded_values':numeric})
            controls+=1

        r['inventory']=cleaned
        if non_inventory: r['decoded_non_inventory_slots']=non_inventory
        else: r.pop('decoded_non_inventory_slots',None)
        r.pop('inventory_decode_anomalies',None)
        r['inventory_projection_status']='authoritative_stock_only_non_item_slots_classified'

        sid=str(r.get('legacy_id') or '').strip()
        expected=matrix.get(sid)
        if expected is not None:
            actual=[str(e['item_ref']).split('.')[-1] for e in cleaned]
            if actual != expected:
                raise RuntimeError(f"{r.get('id')}: stock mismatch vs ShopInventoryEconomyEvidence: actual={actual} expected={expected}")

    p.write_text(json.dumps(doc,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print(f'{p.relative_to(root)}: markup={promoted}; empty fixed slots={empty_removed}; classified non-item controls={controls}')
