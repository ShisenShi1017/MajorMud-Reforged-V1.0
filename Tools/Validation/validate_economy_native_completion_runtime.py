import hashlib,json,pathlib,sys,zipfile
ROOT=pathlib.Path(__file__).resolve().parents[2]
required=[
 'Apps/Source/Reforged.Server/RuntimeEconomyNativeCompletion.cs',
 'Apps/Source/Reforged.Server/RuntimeEconomyNativeCompletionSmokeReport.cs',
 'Librarium/Runtime/Economy/native_completion_catalog_0159.json',
 'Evidence/Economy/economy_native_completion_evidence_0159.json',
 'Docs/Economy/ECONOMY_NATIVE_COMPLETION_RUNTIME_PASS_0159.md']
errors=[]
for rel in required:
 p=ROOT/rel
 if not p.is_file(): errors.append('missing:'+rel)
cat=json.load(open(ROOT/required[2])); ev=json.load(open(ROOT/required[3]))
if cat.get('schemaVersion')!=1: errors.append('schema')
if len(cat.get('banks',[]))!=27: errors.append('bank-count')
if len(cat.get('rules',[]))!=23: errors.append('rule-count')
if len({x['bankId'].lower() for x in cat['banks']})!=27: errors.append('bank-id-duplicate')
if len({x['ruleId'].lower() for x in cat['rules']})!=23: errors.append('rule-id-duplicate')
if not all(x.get('evidenceRefs') for x in cat['banks']): errors.append('bank-evidence')
if not all(x.get('globalRule') or x.get('shopIds') or x.get('itemIds') for x in cat['rules']): errors.append('rule-ownership')
blocking=[x for x in cat['dependencies'] if x['blocksSemanticExhaustion'] and x['state']!='Resolved']
if len(blocking)!=7: errors.append('blocking-dependency-count')
if ev.get('classificationSummary',{}).get('unknown')!=7: errors.append('evidence-unknown-count')
cs=(ROOT/required[0]).read_text()
for token in ['RuntimeEconomyNativeCompletionEngine','BindBankDefinition','RequireResolved','SemanticallyExhausted']:
 if token not in cs: errors.append('csharp-token:'+token)
manifest=ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0159.json'
if manifest.exists():
 m=json.load(open(manifest)); listed=sorted(x['path'] for x in m['files'])
 actual=set(str(p.relative_to(ROOT)).replace('\\','/') for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc')
 if any(rel not in actual for rel in listed): errors.append('manifest-content')
 for row in m['files']:
  if row['path']=='Tools/Validation/validate_economy_native_completion_runtime.py': continue
  p=ROOT/row['path']; h=hashlib.sha256(p.read_bytes()).hexdigest()
  if h!=row['sha256'] or p.stat().st_size!=row['size']: errors.append('manifest-hash:'+row['path'])
if errors:
 print('FAIL',*errors,sep='\n'); sys.exit(1)
print('PASS economy native completion 0159',len(cat['banks']),len(cat['rules']),len(blocking))
