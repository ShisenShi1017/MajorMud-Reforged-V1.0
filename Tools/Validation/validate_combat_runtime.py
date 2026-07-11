import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
server=root/'Apps/Source/Reforged.Server'
errors=[]
required={
 'RuntimeCombat.cs':['RuntimeCombatSession','IRuntimeCombatInitiativePolicy','IRuntimeCombatActionPolicy','IRuntimeCombatEventSink','IRuntimeCombatDeathHandler','RuntimeCombatSnapshot','COMBAT_DUPLICATE_PARTICIPANT','Restore(before)'],
 'RuntimeCombatSmokeReport.cs':['participants','round','initiative','attack','death','corpse-hook','complete','validator','rollback'],
 'Program.cs':['--smoke-combat-runtime','RuntimeCombatSmokeReport.Run()']}
for name,tokens in required.items():
 p=server/name
 if not p.exists(): errors.append(f'missing:{p}'); continue
 text=p.read_text(encoding='utf-8')
 for token in tokens:
  if token not in text: errors.append(f'{name}:missing-token:{token}')
for forbidden in ['MinimumDamage =','MaximumDamage =','HitChance =','DamageFormula =']:
 text=(server/'RuntimeCombat.cs').read_text(encoding='utf-8')
 if forbidden in text: errors.append(f'hardcoded-gameplay-value:{forbidden}')
print(json.dumps({'passed':not errors,'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
