#!/usr/bin/env python3
from pathlib import Path
import json, sys

root = Path(__file__).resolve().parents[2]
resolver = root / 'Apps/Source/Reforged.Server/ClassicCombatResolver.cs'
smoke = root / 'Apps/Source/Reforged.Server/ClassicCombatSmokeReport.cs'
program = root / 'Apps/Source/Reforged.Server/Program.cs'
formulas = root / 'Data/Classic/formulas.json'
errors=[]
for p in (resolver, smoke, program, formulas):
    if not p.exists(): errors.append(f'missing {p.relative_to(root)}')
if not errors:
    r=resolver.read_text(encoding='utf-8')
    required=[
        'ClassicAttackType.Backstab', 'attacker.AttackValue - defender.DefenseValue',
        '100 - (int)((defense * defense) / hitRating)', 'Math.Clamp(chance, 10, 99)',
        'rolledDamage - Math.Max(0, request.Defender.DamageResistance)',
        'chance /= 5', 'ClassicHitOutcome.Glance', 'ClassicHitOutcome.Dodged',
        'ClassicAttackType.Bash => new(-15, 10, false, 3)',
        'ClassicAttackType.Smash => new(-25, 20, false, 5)',
        'ClassicAttackType.Special125 => new(-75, 125, false, 1)',
    ]
    for token in required:
        if token not in r: errors.append(f'missing resolver contract token: {token}')
    if '--smoke-combat' not in program.read_text(encoding='utf-8'):
        errors.append('Program.cs missing --smoke-combat')
    data=json.loads(formulas.read_text(encoding='utf-8'))
    records=data.get('records', data if isinstance(data,list) else [])
    ids={x.get('id') for x in records if isinstance(x,dict)}
    for fid in ('formula.combat.hit_vs_ac','formula.combat.damage_reduction.physical','formula.combat.dodge_order','formula.combat.special_defense_order'):
        if fid not in ids: errors.append(f'generated formula missing {fid}')
if errors:
    print('FAIL CodeUpdate0135 combat validation')
    for e in errors: print(' -',e)
    sys.exit(1)
print('PASS CodeUpdate0135 combat validation')
print('Verified generated formula contracts and runtime resolver source bindings.')
