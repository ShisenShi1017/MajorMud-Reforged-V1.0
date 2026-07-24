import hashlib,json,pathlib,py_compile,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
required=[
'Apps/Source/Reforged.Server/RuntimeEconomyServiceCostMultiplierComposition.cs',
'Apps/Source/Reforged.Server/RuntimeEconomyServiceCostMultiplierCompositionSmokeReport.cs',
'Librarium/Runtime/Economy/service_cost_multiplier_composition_catalog_0163.json',
'Evidence/Economy/economy_service_cost_multiplier_composition_evidence_0163.json',
'Evidence/Economy/economy_service_cost_multiplier_composition_provenance_0163.json',
'Docs/Economy/ECONOMY_SERVICE_COST_MULTIPLIER_COMPOSITION_PASS_0163.md']
errors=[]
for rel in required:
 if not (ROOT/rel).is_file(): errors.append('missing:'+rel)
cat=json.load(open(ROOT/required[2])); ev=json.load(open(ROOT/required[3])); prov=json.load(open(ROOT/required[4]))
if cat.get('schemaVersion')!=1: errors.append('schema')
if cat.get('classification')!='STRONGLY_CORRELATED': errors.append('classification')
if '10000^3' not in cat.get('formula',''): errors.append('formula')
if 'UNKNOWN' not in cat.get('nativeDependency',''): errors.append('native-dependency')
if ev.get('classification')!='STRONGLY_CORRELATED': errors.append('evidence-classification')
if not prov.get('records'): errors.append('provenance')
cs=(ROOT/required[0]).read_text(); trace=(ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyServiceCostTrace.cs').read_text()
for token in ['BigInteger.Pow','BigInteger.DivRem','IsOrderInvariant','service-cost-composition-overflow']:
 if token not in cs: errors.append('csharp-token:'+token)
for token in ['CompositeMultiplierOrderConfirmed','RuntimeEconomyServiceCostMultiplierComposition.Apply']:
 if token not in trace: errors.append('trace-integration:'+token)
# JSON and Python syntax
for p in ROOT.rglob('*.json'):
 try: json.load(open(p))
 except Exception as e: errors.append('json:'+str(p.relative_to(ROOT))+':'+str(e))
for p in ROOT.rglob('*.py'):
 try: py_compile.compile(str(p),doraise=True)
 except Exception as e: errors.append('python:'+str(p.relative_to(ROOT))+':'+str(e))
manifest=ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0163.json'
if manifest.exists():
 m=json.load(open(manifest))
 for row in m['files']:
  if not (ROOT/row['path']).is_file(): errors.append('historical-manifest-missing:'+row['path'])
if errors:
 print('FAIL',*errors,sep='\n');sys.exit(1)
print('PASS economy service cost multiplier composition 0163')
