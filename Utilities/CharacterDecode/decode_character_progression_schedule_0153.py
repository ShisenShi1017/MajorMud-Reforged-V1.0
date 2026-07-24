#!/usr/bin/env python3
import json, sys
from pathlib import Path
root=Path(sys.argv[1] if len(sys.argv)>1 else '.')
keys={'required_experience','experience_required','total_experience'}
found=[]
for path in sorted((root/'Data'/'Classic').glob('*.json')):
    try: data=json.loads(path.read_text(encoding='utf-8'))
    except Exception: continue
    def walk(node, loc='$'):
        if isinstance(node,dict):
            if keys.intersection(node) and ('level' in node or 'target_level' in node):
                found.append({'source_path':str(path.relative_to(root)),'location':loc,'candidate':node,'classification':'weak','runtime_promoted':False})
            for k,v in node.items(): walk(v,loc+'.'+k)
        elif isinstance(node,list):
            for i,v in enumerate(node): walk(v,f'{loc}[{i}]')
    walk(data)
out={'code_update':'0153','candidate_count':len(found),'policy':'Candidates remain non-executable until each row has proven or strongly_correlated classification plus source_id.','candidates':found}
outpath=root/'Data'/'NativeDecodeReports'/'CodeUpdate0153CharacterProgression'/'progression_schedule_candidates_0153.json'
outpath.parent.mkdir(parents=True,exist_ok=True); outpath.write_text(json.dumps(out,indent=2)+'\n')
print(outpath)
