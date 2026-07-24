import hashlib,json,pathlib,py_compile,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
required=[
'Apps/Source/Reforged.Server/RuntimeEconomyServiceCostFinalization.cs',
'Apps/Source/Reforged.Server/RuntimeEconomyServiceCostFinalizationSmokeReport.cs',
'Librarium/Runtime/Economy/service_cost_finalization_catalog_0164.json',
'Evidence/Economy/economy_service_cost_finalization_evidence_0164.json',
'Evidence/Economy/economy_service_cost_finalization_provenance_0164.json',
'Docs/Economy/ECONOMY_SERVICE_COST_FINALIZATION_PASS_0164.md']
errors=[]
for rel in required:
 if not (ROOT/rel).is_file(): errors.append('missing:'+rel)
cat=json.load(open(ROOT/required[2]));ev=json.load(open(ROOT/required[3]));prov=json.load(open(ROOT/required[4]))
if cat.get('schemaVersion')!=1: errors.append('schema')
if cat.get('classification')!='STRONGLY_CORRELATED': errors.append('classification')
if 'UNKNOWN' not in cat.get('nativeDependency',''): errors.append('native-dependency')
if ev.get('classification')!='STRONGLY_CORRELATED': errors.append('evidence-classification')
if not prov.get('records'): errors.append('provenance')
cs=(ROOT/required[0]).read_text();smoke=(ROOT/required[1]).read_text();trace=(ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyServiceCostTrace.cs').read_text()
for token in ['AdditiveBeforeMultipliers','MinimumAfterFinalRounding','native-service-cost-finalization-order-unresolved','BigInteger.Max','service-cost-finalization-overflow']:
 if token not in cs: errors.append('csharp-token:'+token)
for token in ['additive-before:pass','minimum-after:pass','native-unresolved:pass']:
 if token not in smoke: errors.append('smoke-token:'+token)
for token in ['ApplyAdditiveAdjustment','ApplyMinimumCost']:
 if token not in trace: errors.append('trace-stage:'+token)
for p in ROOT.rglob('*.json'):
 try: json.load(open(p))
 except Exception as e: errors.append('json:'+str(p.relative_to(ROOT))+':'+str(e))
for p in ROOT.rglob('*.py'):
 try: py_compile.compile(str(p),doraise=True)
 except Exception as e: errors.append('python:'+str(p.relative_to(ROOT))+':'+str(e))
manifest=ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0164.json'
if manifest.exists():
 m=json.load(open(manifest));listed={x['path']:x for x in m['files']}
 actual={str(p.relative_to(ROOT)).replace('\\','/'):p for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p!=manifest}
 if set(listed)!=set(actual): errors.append('manifest-content')
 for rel,p in actual.items():
  row=listed[rel]
  if row['size']!=p.stat().st_size or row['sha256']!=hashlib.sha256(p.read_bytes()).hexdigest(): errors.append('manifest-hash:'+rel)
if errors:
 print('FAIL',*errors,sep='\n');sys.exit(1)
print('PASS economy service cost finalization 0164')
