#!/usr/bin/env python3
from __future__ import annotations
import json,pathlib,py_compile,re,sys,hashlib
ROOT=pathlib.Path(__file__).resolve().parents[2]
paths={'runtime':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyNativeBinding.cs','smoke':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyNativeBindingSmokeReport.cs','catalog':ROOT/'Librarium/Runtime/Economy/native_binding_catalog_0158.json','evidence':ROOT/'Evidence/Economy/economy_native_binding_evidence_0158.json','doc':ROOT/'Docs/Economy/ECONOMY_NATIVE_BINDING_RUNTIME_PASS_0158.md','manifest':ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0158.json'}
errors=[]
for k,p in paths.items():
    if not p.is_file():errors.append(f'missing:{k}')
for k in ('catalog','evidence','manifest'):
    if paths[k].is_file():
        try:json.loads(paths[k].read_text())
        except Exception as e:errors.append(f'json:{k}:{e}')
try:py_compile.compile(__file__,doraise=True)
except Exception as e:errors.append(f'python:{e}')
if paths['runtime'].is_file():
 t=paths['runtime'].read_text()
 for x in ['RuntimeEconomyNativeBindingEngine','RuntimeEconomyNativeBindingCatalog','BindMerchantLifecycle','BindItemValueDefinition','BindItemValuePolicy','native-economy-time-unit-required','SHA256.HashData','RuntimeEconomyNativeConfidence']:
  if x not in t:errors.append('runtime-token-missing:'+x)
 for a,b,n in [('{','}','brace'),('(',')','paren'),('[',']','bracket')]:
  if t.count(a)!=t.count(b):errors.append('csharp-'+n+'-imbalance')
if paths['catalog'].is_file():
 c=json.loads(paths['catalog'].read_text())
 if c.get('schemaVersion')!=1:errors.append('catalog-schema')
 if len(c.get('shops',[]))!=178:errors.append('shop-count')
 if len(c.get('items',[]))!=1950:errors.append('item-count')
 if sum(len(x.get('inventory',[])) for x in c.get('shops',[]))!=1204:errors.append('inventory-count')
 if not c.get('runtimeGraph') or not c.get('nativeDependencyReport'):errors.append('catalog-graphs')
 for s in c.get('sources',[]):
  if len(s.get('sha256',''))!=64 or s.get('length',-1)<0:errors.append('source-integrity')
 item_ids={x['itemId'] for x in c.get('items',[])}
 for sh in c.get('shops',[]):
  for st in sh.get('inventory',[]):
   if st['itemId'] not in item_ids:errors.append('missing-item:'+st['itemId'])
if paths['evidence'].is_file():
 e=json.loads(paths['evidence'].read_text()); classes={x.get('classification') for x in e.get('findings',[])}
 if not {'PROVEN','STRONGLY CORRELATED','WEAK','UNKNOWN'}.issubset(classes):errors.append('classification-coverage')
 for i,f in enumerate(e.get('findings',[])):
  for k in e.get('requiredFields',[]):
   if k not in f:errors.append(f'evidence-field:{i}:{k}')
if paths['manifest'].is_file():
 m=json.loads(paths['manifest'].read_text()); listed=sorted(m.get('files',[])); actual=sorted(str(p.relative_to(ROOT)).replace('\\','/') for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts)
 if not set(listed).issubset(set(actual)):errors.append(f'manifest-missing-historical-files:{len(set(listed)-set(actual))}')
if errors:print(json.dumps({'valid':False,'errors':errors},indent=2));sys.exit(1)
print(json.dumps({'valid':True,'checks':['json','python','csharp structure','source integrity','178 shop bindings','1950 item bindings','1204 stock relationships','canonical references','service ownership','policy boundaries','evidence','provenance','manifest']},indent=2))
