#!/usr/bin/env python3
import json, os, sys
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
code='0121'
required=[
 f'Librarium/Schemas/librarium_object_schema_{code}.json',
 f'Librarium/Indexes/librarium_metrics_{code}.json',
 f'Librarium/Indexes/librarium_completion_index_{code}.json',
 'Librarium/Worlds/world_branches_0121.json',
 'AGENTS.md','Docs/PROJECT_STATUS.md','Data/README.md']
errors=[]
relationship_candidates=[
 'Librarium/Relationships/librarium_relationship_graph_0121.json',
 'Librarium/Assertions/knowledge_assertions_0011.json',
 'Librarium/SQLDataImport/sql_mdb_relationships_0128.json']
if not any(os.path.exists(os.path.join(ROOT, rel)) for rel in relationship_candidates):
 errors.append('missing current Librarium relationship artifact')
for rel in required:
 p=os.path.join(ROOT,rel)
 if not os.path.exists(p): errors.append(f'missing {rel}')
schema=json.load(open(os.path.join(ROOT,required[0])))
metrics=json.load(open(os.path.join(ROOT,required[1])))
completion=json.load(open(os.path.join(ROOT,required[2])))
if metrics.get('total_files')!=941: errors.append('expected 941 classified files')
if metrics.get('text_files_scanned')!=657: errors.append('expected 657 text files')
if metrics.get('binary_or_asset_files')!=284: errors.append('expected 284 binary/asset files')
if len(schema.get('object_types',[])) < 30: errors.append('expected at least 30 object types')
for obj in schema.get('object_types',[]):
 d=os.path.join(ROOT,obj['directory'])
 if not os.path.isdir(d): errors.append(f'missing object directory {obj["directory"]}')
 if not os.path.exists(os.path.join(d,'catalog.json')): errors.append(f'missing catalog for {obj["object_type"]}')
if completion.get('phase2')!='complete': errors.append('phase2 not complete')
if not str(completion.get('phase3','')).startswith('complete'): errors.append('phase3 not complete')
if errors:
 print('FAIL')
 for e in errors: print('-',e)
 sys.exit(1)
print('PASS CodeUpdate 0121 Phase 2/3 Librarium completion validation')
print(json.dumps({k:metrics[k] for k in ['total_files','text_files_scanned','binary_or_asset_files','raw_evidence_count','object_type_count','open_conflict_count']}, indent=2))
