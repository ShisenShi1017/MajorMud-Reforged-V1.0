import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
errors=[]
for rel in ['Apps/Source/Reforged.Server/RuntimeActorsAbilities.cs','Apps/Source/Reforged.Server/RuntimeActorsAbilitiesSmokeReport.cs','Data/Classic/skills.json','Data/Classic/npcs.json']:
    if not (root/rel).is_file(): errors.append('missing:'+rel)
try:
    skills=json.load(open(root/'Data/Classic/skills.json',encoding='utf-8'))
    npcs=json.load(open(root/'Data/Classic/npcs.json',encoding='utf-8'))
    if skills.get('record_count') != len(skills.get('records',[])): errors.append('skill-count-mismatch')
    if npcs.get('record_count') != len(npcs.get('records',[])): errors.append('npc-count-mismatch')
except Exception as ex: errors.append('json:'+str(ex))
text=(root/'Apps/Source/Reforged.Server/RuntimeActorsAbilities.cs').read_text(encoding='utf-8') if (root/'Apps/Source/Reforged.Server/RuntimeActorsAbilities.cs').exists() else ''
for token in ['IRuntimeAbilityPolicy','IRuntimeTrainerPolicy','IRuntimeNpcLifecyclePolicy','RuntimeAbilityEngine','RuntimeNpcRegistry','Restore(','Validate(']:
    if token not in text: errors.append('missing-contract:'+token)
print(json.dumps({'passed':not errors,'skill_count':locals().get('skills',{}).get('record_count',0),'npc_count':locals().get('npcs',{}).get('record_count',0),'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
