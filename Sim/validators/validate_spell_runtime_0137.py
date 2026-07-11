import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
errors=[]
path=root/'Data'/'Classic'/'spells.json'
try:
    doc=json.loads(path.read_text(encoding='utf-8'))
    records=doc.get('records',[])
    if not records: errors.append('generated spells records missing')
    ids=[r.get('id') for r in records]
    if len(ids)!=len(set(ids)): errors.append('duplicate generated spell ids')
    for r in records:
        for group in ('classification','costs','effect'):
            if not isinstance(r.get(group),dict): errors.append(f"{r.get('id')}: missing {group}")
        if r.get('runtime_mapping',{}).get('runtime_reads_generated_data') is not True:
            errors.append(f"{r.get('id')}: runtime mapping is not generated-data authoritative")
except Exception as exc: errors.append(str(exc))
required=[
'Apps/Source/Reforged.Server/RuntimeSpells.cs',
'Apps/Source/Reforged.Server/RuntimeSpellSmokeReport.cs',
]
for rel in required:
    if not (root/rel).is_file(): errors.append(f'missing {rel}')
program=(root/'Apps/Source/Reforged.Server/Program.cs').read_text(encoding='utf-8')
if '--smoke-spell-runtime' not in program: errors.append('spell smoke command not registered')
print(json.dumps({'passed':not errors,'spell_count':len(records) if 'records' in locals() else 0,'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
