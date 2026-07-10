#!/usr/bin/env python3
from pathlib import Path
import json, collections, sys
ROOT=Path(__file__).resolve().parents[2]
errors=[]
def records(rel):
 d=json.loads((ROOT/rel).read_text(encoding='utf-8')); return d.get('records',[])
# triggers unique
tr=records('Data/Classic/triggers.json'); c=collections.Counter(x.get('id') for x in tr)
errors += [f'duplicate trigger id {k}' for k,v in c.items() if k and v>1]
# no fake room
rooms=records('Data/Classic/rooms.json')
if any(r.get('id')=='room.unknown' for r in rooms): errors.append('room.unknown remains in generated runtime rooms')
if any(not r.get('mapId') and not r.get('map_id') for r in rooms): errors.append('generated room without map identity remains')
# bug records
npcs={str(r.get('legacy_id')):r for r in records('Data/Classic/npcs.json')}
if npcs.get('1086',{}).get('rewards',{}).get('xp_base')!='10000': errors.append('cloaked figure npc.1086 xp is not 10000')
items={str(r.get('legacy_id')):r for r in records('Data/Classic/items.json')}
for i in ('288','561','999','1884'):
 if items.get(i,{}).get('equipment',{}).get('slot_name')!='face': errors.append(f'mask item {i} is not explicitly face-slot')
if items.get('1317',{}).get('use_effect',{}).get('spell_ref')!='spell.1055': errors.append("lyrist's companion does not invoke spell.1055")
spells={str(r.get('legacy_id')):r for r in records('Data/Classic/spells.json')}
m=spells.get('1055',{}).get('messages',{})
if m.get('cast_message_id')!='2520' or m.get('description_message_id')!='2531': errors.append('BCNS message hooks are not 2520/2531')
rules={r.get('id'):r for r in records('Data/Classic/economy_rules.json')}
if rules.get('economy_rule.bard_shop_minimum_percent',{}).get('minimum_percent')!=10: errors.append('bard shop minimum is not 10%')
fixes={r.get('id'):r for r in records('Data/Classic/classic_bug_fixes.json')}
for req in ['classic_fix.hidden_sneak_visibility','classic_fix.two_handed_backstab_downgrade','classic_fix.dual_lever_atomicity','classic_fix.gang_deed_cleanup']:
 if req not in fixes: errors.append(f'missing runtime correction contract {req}')
if errors:
 print('FAIL CodeUpdate0135 exhaustive repairs')
 for e in errors: print(' -',e)
 sys.exit(1)
print('PASS CodeUpdate0135 exhaustive repairs')
print(f'triggers={len(tr)} rooms={len(rooms)} classic_bug_fixes={len(fixes)}')
