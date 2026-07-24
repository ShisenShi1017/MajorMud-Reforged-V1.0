#!/usr/bin/env python3
from __future__ import annotations
import json,pathlib,py_compile,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
paths={
'runtime':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyLimitedItems.cs','smoke':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyLimitedItemsSmokeReport.cs',
'commerce':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyCommerce.cs','loot':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyLoot.cs',
'catalog':ROOT/'Librarium/Runtime/Economy/limited_item_runtime_catalog_0156.json','evidence':ROOT/'Evidence/Economy/economy_limited_item_runtime_evidence_0156.json',
'doc':ROOT/'Docs/Economy/ECONOMY_LIMITED_ITEM_RUNTIME_PASS_0156.md','manifest':ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0156.json'}
errors=[]
for k,p in paths.items():
    if not p.is_file():errors.append(f'missing:{k}:{p.relative_to(ROOT)}')
for k in ('catalog','evidence','manifest'):
    if paths[k].is_file():
        try:json.loads(paths[k].read_text())
        except Exception as e:errors.append(f'json:{k}:{e}')
try:py_compile.compile(__file__,doraise=True)
except Exception as e:errors.append(f'python:{e}')
if paths['runtime'].is_file():
    t=paths['runtime'].read_text()
    required=['RuntimeLimitedItemPolicy','RuntimeLimitedItemLedger','RuntimeLimitedItemAllocation','RuntimeLimitedItemState','Reserve(','Activate(','Release(','Destroy(','ProcessRespawns(','ValidateMerchantIntegration','ValidateLootIntegration','limited-item-world-limit-exceeded','limited-item-owner-unique-limit','limited-item-replay-detected','ledger.RestoreUnsafe(before)','persistence.Persist','CurrentSchemaVersion=1','checked(ledger.CountCommittedUnsafe']
    for x in required:
        if x not in t:errors.append(f'runtime-token-missing:{x}')
    for a,b,n in [('{','}','brace'),('(',')','paren'),('[',']','bracket')]:
        if t.count(a)!=t.count(b):errors.append(f'csharp-{n}-imbalance')
    defs=set(re.findall(r'public\s+(?:sealed\s+)?(?:class|record|interface|enum|static\s+class)\s+(\w+)',t))
    for x in ['RuntimeLimitedItemLedgerSnapshot','RuntimeLimitedItemResult','IRuntimeLimitedItemPersistence','RuntimeLimitedItemStore']:
        if x not in defs:errors.append(f'csharp-definition-missing:{x}')
for key,tokens in [('commerce',['LimitedItem','RuntimeMerchantStock']),('loot',['RuntimeLootCorpse','RuntimeLootTableDefinition'])]:
    if paths[key].is_file():
        text=paths[key].read_text()
        for x in tokens:
            if x not in text:errors.append(f'{key}-integration-missing:{x}')
if paths['smoke'].is_file():
    s=paths['smoke'].read_text()
    for x in ['world-limit-reservation-and-overflow-protection','unique-owner-enforcement','merchant-and-loot-eligibility-integration','atomic-ledger-rollback-on-persistence-failure','versioned-limited-item-persistence']:
        if x not in s:errors.append(f'smoke-token-missing:{x}')
if paths['catalog'].is_file():
    c=json.loads(paths['catalog'].read_text())
    for k in ['limitedItemCatalog','worldLimitCatalog','reservationCatalog','ownershipCatalog','releaseCatalog','destructionCatalog','recyclingCatalog','respawnCatalog','merchantIntegrationCatalog','lootIntegrationCatalog','ownershipGraph','runtimeGraph','saveImpact','worldImpact','nativeDependencyReport']:
        if not c.get(k):errors.append(f'catalog-missing:{k}')
    for e in ['item<->policy','item<->allocation','allocation<->owner','merchant<->limited-item','loot-table<->limited-item','allocation<->replay-key']:
        if e not in c.get('ownershipGraph',{}).get('edges',[]):errors.append(f'catalog-edge-missing:{e}')
if paths['evidence'].is_file():
    e=json.loads(paths['evidence'].read_text());findings=e.get('findings',[]);classes={x.get('classification') for x in findings}
    if not {'PROVEN','STRONGLY CORRELATED','WEAK','UNKNOWN'}.issubset(classes):errors.append('evidence-classification-coverage')
    for i,f in enumerate(findings):
        for k in e.get('requiredFields',[]):
            if k not in f:errors.append(f'evidence-field-missing:{i}:{k}')
if paths['manifest'].is_file():
    m=json.loads(paths['manifest'].read_text());files=m.get('files',[])
    if len(files)!=len(set(files)):errors.append('manifest-duplicate-file')
    actual=sorted(str(p.relative_to(ROOT)).replace('\\','/') for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts)
    missing=sorted(set(files)-set(actual))
    if missing:errors.append(f'manifest-files-missing:{missing}')
if errors:print(json.dumps({'valid':False,'errors':errors},indent=2));sys.exit(1)
print(json.dumps({'valid':True,'checks':['json','python','csharp-structure','world limits','unique ownership','reservation','activation','release','destruction','recycling','respawn','merchant integration','loot integration','rollback','replay protection','persistence','librarium relationships','evidence','provenance','manifest']},indent=2))
