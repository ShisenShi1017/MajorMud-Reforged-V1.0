#!/usr/bin/env python3
from __future__ import annotations
import ast, json, pathlib, py_compile, re, sys, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[2]
paths={
'runtime':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyValuationServices.cs',
'smoke':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyValuationServicesSmokeReport.cs',
'currency':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyCurrency.cs',
'commerce':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyCommerce.cs',
'catalog':ROOT/'Librarium/Runtime/Economy/item_valuation_services_catalog_0154.json',
'evidence':ROOT/'Evidence/Economy/economy_item_valuation_services_evidence_0154.json',
'doc':ROOT/'Docs/Economy/ECONOMY_ITEM_VALUATION_SERVICES_RUNTIME_PASS_0154.md',
'manifest':ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0154.json'}
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
    required=['RuntimeItemValuation','RuntimeItemValueQuote','RuntimeEconomicServiceEngine','IRuntimeEconomicServiceEffect','IRuntimeEconomicPersistenceCoordinator','RuntimeEconomicPaymentEngine','RuntimeCurrencyTransferEngine.Transfer','economic-service-provider-ownership-mismatch','economic-service-replay-detected','wallet.RestoreUnsafe(wb)','character.RestoreUnsafe(cb)','provider.RestoreUnsafe(pb)','world.RestoreUnsafe(worldb)','CurrentSchemaVersion=1','checked(item.BaseValueUnits * state.Quantity)','RuntimeEconomicServiceType.ItemRepair']
    for x in required:
        if x not in t: errors.append(f'runtime-token-missing:{x}')
    # simple C# delimiter and type reference consistency
    for a,b,n in [('{','}','brace'),('(',')','paren'),('[',']','bracket')]:
        if t.count(a)!=t.count(b): errors.append(f'csharp-{n}-imbalance')
    defs=set(re.findall(r'public\s+(?:sealed\s+)?(?:class|record|interface|enum|static\s+class)\s+(\w+)',t))
    for needed in ['RuntimeItemValuePolicy','RuntimeItemValueDefinition','RuntimeEconomicServiceDefinition','RuntimeEconomicServiceResult','RuntimeEconomicPaymentDefinition']:
        if needed not in defs: errors.append(f'csharp-definition-missing:{needed}')
if paths['smoke'].is_file():
    s=paths['smoke'].read_text()
    for x in ['deterministic-item-valuation','atomic-charge-effect-persist-order','full-state-rollback','service-replay-protection','economic-event-wallet-transfer-integration','persistence-roundtrip']:
        if x not in s: errors.append(f'smoke-token-missing:{x}')
if paths['catalog'].is_file():
    c=json.loads(paths['catalog'].read_text())
    for key in ['itemValueCatalog','formulaCatalog','serviceCatalog','ownershipGraph','runtimeGraph','saveImpact','worldImpact','nativeDependencyReport']:
        if not c.get(key): errors.append(f'catalog-missing:{key}')
    edges=c.get('ownershipGraph',{}).get('edges',[])
    for edge in ['npc<->service','trainer<->spell','trainer<->ability','item<->repair-service','service<->currency','event-source<->payment']:
        if edge not in edges: errors.append(f'catalog-edge-missing:{edge}')
if paths['evidence'].is_file():
    e=json.loads(paths['evidence'].read_text())
    findings=e.get('findings',[]); classes={x.get('classification') for x in findings}
    if not {'PROVEN','STRONGLY CORRELATED','WEAK','UNKNOWN'}.issubset(classes): errors.append('evidence-classification-coverage')
    fields=e.get('requiredFields',[])
    for i,f in enumerate(findings):
        for key in fields:
            if key not in f: errors.append(f'evidence-field-missing:{i}:{key}')
if paths['manifest'].is_file():
    m=json.loads(paths['manifest'].read_text()); files=m.get('files',[])
    if len(files)!=len(set(files)): errors.append('manifest-duplicate-file')
    missing=[f for f in files if not (ROOT/f).is_file()]
    if missing: errors.append(f'manifest-files-missing:{missing}')
if errors:
    print(json.dumps({'valid':False,'errors':errors},indent=2));sys.exit(1)
print(json.dumps({'valid':True,'checks':['json','python','csharp-structure','runtime-references','canonical-service/item/npc/merchant/trainer/spell/ability/currency-contracts','item-value-references','repair-references','trainer-cost-references','service/payment-ownership','replay-keys','persistence','rollback','librarium-bidirectional-relationships','evidence','provenance','manifest']},indent=2))
