import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else r'E:\MajorMud-Reforged Github')
errors=[]
files=[root/'Apps/Source/Reforged.Server/RuntimeSpells.cs',root/'Apps/Source/Reforged.Server/RuntimeSpellExecution.cs',root/'Apps/Source/Reforged.Server/RuntimeSpellExecutionSmokeReport.cs']
for f in files:
    if not f.is_file(): errors.append(f'missing: {f.relative_to(root)}')
text=(files[1].read_text(encoding='utf-8') if files[1].is_file() else '')
for token in ['IRuntimeSpellTargetPolicy','IRuntimeSpellOperationPolicy','IRuntimeSpellEventSink','RuntimeSpellExecutionEngine','RestoreSnapshot','Dispel','Tick']:
    if token not in text and token != 'RestoreSnapshot': errors.append(f'missing contract: {token}')
runtime=(files[0].read_text(encoding='utf-8') if files[0].is_file() else '')
if 'RestoreSnapshot' not in runtime: errors.append('missing atomic restore contract')
spells=root/'Data/Classic/spells.json'
count=0
if spells.is_file():
    data=json.loads(spells.read_text(encoding='utf-8-sig')); count=len(data.get('records',[]))
    if count == 0: errors.append('generated spell catalog is empty')
else: errors.append('missing generated spell catalog')
print(json.dumps({'passed':not errors,'spell_count':count,'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
