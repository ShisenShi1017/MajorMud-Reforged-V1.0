#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
required={
 "commands":"command_records_0123.json",
 "status_effects":"status_effect_records_0123.json",
 "economy_rules":"economy_rule_records_0123.json",
 "banks":"bank_records_0123.json",
 "classes":"class_records_0123.json",
 "races":"race_records_0123.json",
 "guilds":"guild_records_0123.json",
 "skills":"skill_records_0123.json",
 "events":"event_records_0123.json",
 "triggers":"trigger_records_0123.json",
 "quests":"quest_records_0123.json",
 "ai_profiles":"ai_profile_records_0123.json",
 "worlds":"world_records_0123.json",
 "world_overlays":"world_overlay_records_0123.json",
 "formulas":"formula_records_0123.json"
}
errors=[]; counts={}
for folder,fname in required.items():
    p=root/'Librarium'/'Objects'/folder/fname
    if not p.exists():
        errors.append(f'missing {p}')
        continue
    try:
        data=json.loads(p.read_text(encoding='utf-8'))
    except Exception as exc:
        errors.append(f'invalid json {p}: {exc}')
        continue
    count=data.get('record_count',0)
    counts[folder]=count
    if count<=0:
        # world_overlays may be only two records; zero is never okay
        errors.append(f'empty {p}')
    if len(data.get('records',[])) != count:
        errors.append(f'record_count mismatch {p}')
relationship_candidates=[
    root/'Librarium'/'Relationships'/'librarium_relationship_graph_0123.json',
    root/'Librarium'/'Assertions'/'knowledge_assertions_0011.json',
    root/'Librarium'/'SQLDataImport'/'sql_mdb_relationships_0128.json',
]
rel=next((p for p in relationship_candidates if p.exists()), None)
if rel is None:
    errors.append('missing current relationship artifact')
else:
    r=json.loads(rel.read_text(encoding='utf-8'))
    count = len(r) if isinstance(r,list) else r.get('relationship_count',r.get('relationshipCount',len(r.get('relationships',[]))))
    counts['relationships_added']=count
    if count<=0:
        errors.append('empty relationship artifact')
# source-of-truth invariants
for required_path in ['AGENTS.md','Docs/PROJECT_STATUS.md','Data/README.md','ImportedDatabases']:
    if not (root/required_path).exists():
        errors.append(f'missing invariant path {required_path}')
if errors:
    print('FAIL')
    for e in errors:
        print(e)
    sys.exit(1)
print('PASS')
print(json.dumps(counts, indent=2))
