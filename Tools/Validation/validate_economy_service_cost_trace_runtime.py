import hashlib,json,pathlib,py_compile,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
required=['Apps/Source/Reforged.Server/RuntimeEconomyServiceCostTrace.cs','Apps/Source/Reforged.Server/RuntimeEconomyServiceCostTraceSmokeReport.cs','Librarium/Runtime/Economy/service_cost_trace_catalog_0161.json','Evidence/Economy/economy_service_cost_trace_evidence_0161.json','Evidence/Economy/economy_service_cost_trace_provenance_0161.json','Docs/Economy/ECONOMY_SERVICE_COST_TRACE_RUNTIME_PASS_0161.md']
errors=[]
for rel in required:
 if not (ROOT/rel).is_file(): errors.append('missing:'+rel)
cat=json.load(open(ROOT/required[2]));ev=json.load(open(ROOT/required[3]));prov=json.load(open(ROOT/required[4]))
if cat.get('schemaVersion')!=1: errors.append('schema')
if len(cat.get('trainers',[]))!=44: errors.append('trainer-count')
if len(cat.get('spellServices',[]))!=36: errors.append('spell-service-count')
if len(cat.get('executionOrder',[]))!=13: errors.append('order-count')
if [x['sequence'] for x in cat['executionOrder']]!=list(range(1,14)): errors.append('order-sequence')
if cat.get('classification',{}).get('nativeFormulaOrder')!='UNKNOWN_FAIL_CLOSED': errors.append('native-order-invented')
if any(x.get('serviceType')!=8 for x in cat['trainers']): errors.append('trainer-type')
if any(x.get('serviceType')!=10 for x in cat['spellServices']): errors.append('spell-type')
if ev.get('activation','').find('fail-closed')<0: errors.append('activation')
if not prov.get('records'): errors.append('provenance')
cs=(ROOT/required[0]).read_text()
for token in ['ResolveBaseCost','ApplyProviderMarkup','NativeExactOrderConfirmed','native-service-cost-order-unresolved','ValidateFunds','CommitReplay']:
 if token not in cs: errors.append('csharp-token:'+token)
manifest=ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0161.json'
if manifest.exists():
 m=json.load(open(manifest));listed={x['path']:x for x in m['files']}
 actual={str(p.relative_to(ROOT)).replace('\\','/'):p for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p!=manifest}
 for rel,row in listed.items():
  p=ROOT/rel
  if not p.is_file(): errors.append('manifest-missing:'+rel)
if errors:
 print('FAIL',*errors,sep='\n');sys.exit(1)
print('PASS economy service cost trace 0161',len(cat['trainers']),len(cat['spellServices']))
