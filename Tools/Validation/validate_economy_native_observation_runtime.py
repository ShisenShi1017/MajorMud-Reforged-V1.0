import hashlib,json,pathlib,py_compile,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
required=[
'Apps/Source/Reforged.Server/RuntimeEconomyNativeObservation.cs',
'Apps/Source/Reforged.Server/RuntimeEconomyNativeObservationSmokeReport.cs',
'Librarium/Runtime/Economy/native_observation_catalog_0160.json',
'Evidence/Economy/economy_native_observation_evidence_0160.json',
'Evidence/Economy/economy_native_observation_provenance_0160.json',
'Docs/Economy/ECONOMY_NATIVE_OBSERVATION_RUNTIME_PASS_0160.md']
errors=[]
for rel in required:
 if not (ROOT/rel).is_file(): errors.append('missing:'+rel)
cat=json.load(open(ROOT/required[2])); ev=json.load(open(ROOT/required[3])); prov=json.load(open(ROOT/required[4]))
if cat.get('schemaVersion')!=1: errors.append('schema')
if len(cat.get('sources',[]))!=3: errors.append('source-count')
if len(cat.get('dependencies',[]))!=7: errors.append('dependency-count')
if cat.get('assertions')!=[]: errors.append('unsupported-observation-invented')
if any(x.get('state')!='INCOMPLETE' or x.get('activation')!='FAIL_CLOSED' for x in cat['dependencies']): errors.append('dependency-not-fail-closed')
if any(len(x.get('sha256',''))!=64 or x.get('length',0)<=0 for x in cat['sources']): errors.append('source-fingerprint')
if ev.get('classificationSummary',{}).get('unknown')!=7: errors.append('unknown-count')
if not prov.get('records'): errors.append('provenance')
cs=(ROOT/required[0]).read_text()
for token in ['ValidateAndClassify','Promote','VerifySource','Contradicted','Observed']:
 if token not in cs: errors.append('csharp-token:'+token)
manifest=ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0160.json'
if manifest.exists():
 m=json.load(open(manifest)); listed={x['path']:x for x in m['files']}
 actual={str(p.relative_to(ROOT)).replace('\\','/'):p for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p!=manifest}
 if not set(listed).issubset(set(actual)): errors.append('manifest-content')
 for rel,row in listed.items():
  if rel=='Tools/Validation/validate_economy_native_observation_runtime.py': continue
  p=actual.get(rel)
  if p is None or row['size']!=p.stat().st_size or row['sha256']!=hashlib.sha256(p.read_bytes()).hexdigest(): errors.append('manifest-hash:'+rel)
if errors:
 print('FAIL',*errors,sep='\n');sys.exit(1)
print('PASS economy native observation 0160',len(cat['sources']),len(cat['dependencies']))
