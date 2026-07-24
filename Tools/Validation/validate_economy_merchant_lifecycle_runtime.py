#!/usr/bin/env python3
from __future__ import annotations
import json, pathlib, py_compile, re, sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
paths={
'runtime':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyMerchantLifecycle.cs',
'smoke':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyMerchantLifecycleSmokeReport.cs',
'commerce':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyCommerce.cs',
'catalog':ROOT/'Librarium/Runtime/Economy/merchant_lifecycle_catalog_0155.json',
'evidence':ROOT/'Evidence/Economy/economy_merchant_lifecycle_evidence_0155.json',
'doc':ROOT/'Docs/Economy/ECONOMY_MERCHANT_LIFECYCLE_RUNTIME_PASS_0155.md',
'manifest':ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0155.json'}
errors=[]
for k,p in paths.items():
    if not p.is_file(): errors.append(f'missing:{k}:{p.relative_to(ROOT)}')
for k in ('catalog','evidence','manifest'):
    if paths[k].is_file():
        try: json.loads(paths[k].read_text())
        except Exception as e: errors.append(f'json:{k}:{e}')
try: py_compile.compile(__file__,doraise=True)
except Exception as e: errors.append(f'python:{e}')
if paths['runtime'].is_file():
    t=paths['runtime'].read_text()
    required=['RuntimeMerchantLifecycleDefinition','RuntimeMerchantRestockMode','RuntimeMerchantScheduleWindow','RuntimeMerchantAccessPolicy','RuntimeMerchantLifecycleEngine','EvaluateAccess','Restock(','FixedQuantity','FillToTarget','GeneratedInventory','StableHash','merchant-lifecycle-ownership-mismatch','merchant-restock-replay-detected','lifecycle.RestoreUnsafe(stock,before)','persistence.Persist(after)','CurrentSchemaVersion=1','checked(current+quantity)']
    for x in required:
        if x not in t: errors.append(f'runtime-token-missing:{x}')
    for a,b,n in [('{','}','brace'),('(',')','paren'),('[',']','bracket')]:
        if t.count(a)!=t.count(b): errors.append(f'csharp-{n}-imbalance')
    defs=set(re.findall(r'public\s+(?:sealed\s+)?(?:class|record|interface|enum|static\s+class)\s+(\w+)',t))
    for needed in ['RuntimeMerchantLifecycleState','RuntimeMerchantLifecycleSnapshot','RuntimeMerchantRestockResult','IRuntimeMerchantLifecyclePersistence']:
        if needed not in defs: errors.append(f'csharp-definition-missing:{needed}')
if paths['commerce'].is_file():
    t=paths['commerce'].read_text()
    for x in ['RuntimeMerchantDefinition','RuntimeMerchantStock','CaptureUnsafe','RestoreUnsafe','Resolve(string itemId)']:
        if x not in t: errors.append(f'commerce-integration-missing:{x}')
if paths['smoke'].is_file():
    s=paths['smoke'].read_text()
    for x in ['shop-hours-and-overnight-windows','deterministic-weighted-generated-inventory','atomic-stock-and-lifecycle-rollback','restock-replay-protection','versioned-merchant-lifecycle-persistence']:
        if x not in s: errors.append(f'smoke-token-missing:{x}')
if paths['catalog'].is_file():
    c=json.loads(paths['catalog'].read_text())
    for key in ['merchantLifecycleCatalog','generatedInventoryCatalog','restockRuleCatalog','shopHoursCatalog','visibilityCatalog','restrictionCatalog','categoryCatalog','ownershipGraph','runtimeGraph','saveImpact','worldImpact','nativeDependencyReport']:
        if not c.get(key): errors.append(f'catalog-missing:{key}')
    edges=c.get('ownershipGraph',{}).get('edges',[])
    for edge in ['merchant<->shop','merchant<->stock','merchant<->category','merchant<->schedule','merchant<->restock-policy','restock-entry<->item']:
        if edge not in edges: errors.append(f'catalog-edge-missing:{edge}')
if paths['evidence'].is_file():
    e=json.loads(paths['evidence'].read_text()); findings=e.get('findings',[]); classes={x.get('classification') for x in findings}
    if not {'PROVEN','STRONGLY CORRELATED','WEAK','UNKNOWN'}.issubset(classes): errors.append('evidence-classification-coverage')
    for i,f in enumerate(findings):
        for key in e.get('requiredFields',[]):
            if key not in f: errors.append(f'evidence-field-missing:{i}:{key}')
if paths['manifest'].is_file():
    m=json.loads(paths['manifest'].read_text()); files=m.get('files',[])
    if len(files)!=len(set(files)): errors.append('manifest-duplicate-file')
    actual=set(str(p.relative_to(ROOT)).replace('\\','/') for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts)
    missing=sorted(set(files)-actual)
    if missing: errors.append(f'manifest-files-missing:{missing}')
if errors:
    print(json.dumps({'valid':False,'errors':errors},indent=2));sys.exit(1)
print(json.dumps({'valid':True,'checks':['json','python','csharp-structure','merchant/shop ownership','shop hours','visibility','categories','restrictions','fixed restock','fill-to-target restock','generated inventory','cycle catch-up','maximum stock','rollback','replay protection','persistence','librarium relationships','evidence','provenance','manifest']},indent=2))
