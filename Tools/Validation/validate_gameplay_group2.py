import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.')
errors=[]
for f in ['Apps/Source/Reforged.Server/RuntimeInteractionNarrative.cs','Apps/Source/Reforged.Server/RuntimeInteractionNarrativeSmokeReport.cs']:
 if not (root/f).is_file(): errors.append('missing:'+f)
counts={}
for family in ['quests','scripts']:
 p=root/'Data/Classic'/f'{family}.json'
 try:
  d=json.loads(p.read_text(encoding='utf-8')); counts[family]=len(d.get('records',[]))
  if counts[family]==0: errors.append('empty:'+family)
 except Exception as e: errors.append(f'{family}:{e}')
print(json.dumps({'passed':not errors,'quest_count':counts.get('quests',0),'text_record_count':counts.get('scripts',0),'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
