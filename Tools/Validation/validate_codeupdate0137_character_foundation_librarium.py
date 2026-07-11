#!/usr/bin/env python3
from __future__ import annotations
import json, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]

def main() -> int:
    subprocess.run([sys.executable, str(ROOT/'Tools/Librarium/compile_character_foundation_0137.py'), '--root', str(ROOT)], check=True)
    foundation=json.loads((ROOT/'Data/Classic/character_foundation.json').read_text())
    errors=[]
    races=foundation.get('races',[]); classes=foundation.get('classes',[]); pairs=foundation.get('compatibility',[])
    if foundation.get('authority')!='Librarium': errors.append('authority is not Librarium')
    if len(races)!=13: errors.append(f'expected 13 races; found {len(races)}')
    if len(classes)!=15: errors.append(f'expected 15 classes; found {len(classes)}')
    if len(pairs)!=195: errors.append(f'expected 195 pairs; found {len(pairs)}')
    if any(row.get('runtime_allowed') is not True for row in pairs): errors.append('not every race/class pair is allowed')
    gate=foundation.get('creation_gate') or {}
    if gate.get('ready') is not True: errors.append('creation gate is not ready')
    if gate.get('blockers'): errors.append(f"creation blockers remain: {gate.get('blockers')}")
    for r in races:
        if set((r.get('minimum_stats') or {})) != {'int','wil','str','hea','agl','cha'}: errors.append(f"bad minimum stats {r.get('id')}")
        if set((r.get('maximum_stats') or {})) != {'int','wil','str','hea','agl','cha'}: errors.append(f"bad maximum stats {r.get('id')}")
        if not isinstance(r.get('hp_per_level_adjustment'),int): errors.append(f"missing HP adjustment {r.get('id')}")
        if not isinstance(r.get('ability_slots'),list): errors.append(f"missing race abilities {r.get('id')}")
    for c in classes:
        hp=c.get('hit_points') or {}; magic=c.get('magic') or {}
        if not all(isinstance(hp.get(k),int) for k in ('minimum_per_level','maximum_bonus_per_level','maximum_per_level')): errors.append(f"bad HP bounds {c.get('id')}")
        if not all(isinstance(magic.get(k),int) for k in ('type','level')): errors.append(f"bad magic mapping {c.get('id')}")
        if not isinstance(c.get('ability_slots'),list): errors.append(f"missing class abilities {c.get('id')}")
    rules=foundation.get('character_creation_rules') or {}
    if rules.get('all_race_class_combinations_allowed') is not True: errors.append('all-combo rule missing')
    if rules.get('starting_inventory') != []: errors.append('starting inventory must be empty')
    if rules.get('starting_equipment') != {}: errors.append('starting equipment must be empty')
    if (foundation.get('start_location') or {}).get('room_id')!='room.2147': errors.append('generated Newhaven start is not room.2147')
    if errors:
        print('FAIL CodeUpdate0137 Librarium character foundation'); [print('- '+e) for e in errors]; return 1
    print('PASS CodeUpdate0137 Librarium character foundation')
    print('13 races; 15 classes; all 195 combinations allowed; race/class ability trees promoted')
    print('Starting inventory/equipment empty; HP/mana formulas and Newhaven start generated')
    return 0
if __name__=='__main__': raise SystemExit(main())
