import hashlib,json,pathlib,py_compile,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
required=['Apps/Source/Reforged.Server/RuntimeEconomyNativeMarkup.cs','Apps/Source/Reforged.Server/RuntimeEconomyNativeMarkupSmokeReport.cs','Librarium/Runtime/Economy/native_markup_catalog_0162.json','Evidence/Economy/economy_native_markup_evidence_0162.json','Evidence/Economy/economy_native_markup_provenance_0162.json','Docs/Economy/ECONOMY_NATIVE_MARKUP_RUNTIME_PASS_0162.md']
errors=[]
for rel in required:
 if not (ROOT/rel).is_file(): errors.append('missing:'+rel)
cat=json.load(open(ROOT/required[2]));ev=json.load(open(ROOT/required[3]));prov=json.load(open(ROOT/required[4]))
if cat.get('schemaVersion')!=1: errors.append('schema')
if len(cat.get('bindings',[]))!=178: errors.append('binding-count')
if any(x['multiplierBasisPoints']!=x['rawMarkupPercent']*100 for x in cat['bindings']): errors.append('conversion')
if not any(x['rawMarkupPercent']==100 and x['multiplierBasisPoints']==10000 for x in cat['bindings']): errors.append('neutral')
if cat.get('orderingState','').find('UNKNOWN_FAIL_CLOSED')<0: errors.append('ordering-invented')
if ev.get('classification')!='STRONGLY_CORRELATED': errors.append('classification')
if not prov.get('records'): errors.append('provenance')
cs=(ROOT/required[0]).read_text(); service=(ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyServiceCostTrace.cs').read_text()
for token in ['NeutralMarkupPercent = 100','rawMarkupPercent * 100','RuntimeEconomyCheckedMath.ApplyBasisPoints','native-markup-negative']:
 if token not in cs: errors.append('csharp-token:'+token)
if 'RuntimeEconomyNativeMarkup.Apply(value,policy.ProviderMarkupPercent' not in service: errors.append('service-integration')
if '10000+policy.ProviderMarkupPercent*100' in service: errors.append('additive-markup-regression')
manifest=ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0162.json'
if manifest.exists():
 m=json.load(open(manifest)); listed={x['path']:x for x in m['files']}
 actual={str(p.relative_to(ROOT)).replace('\\','/'):p for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p!=manifest}
 missing=set(listed)-set(actual)
 if missing: errors.append('manifest-missing:'+','.join(sorted(missing)))
 # Historical manifests prove cumulative path preservation. Later passes may intentionally modify a listed file.
if errors:
 print('FAIL',*errors,sep='\n');sys.exit(1)
print('PASS economy native markup 0162',len(cat['bindings']))
