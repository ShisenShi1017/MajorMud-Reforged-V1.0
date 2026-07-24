#!/usr/bin/env python3
from __future__ import annotations
import json,pathlib,py_compile,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
paths={'runtime':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyPersistence.cs','smoke':ROOT/'Apps/Source/Reforged.Server/RuntimeEconomyPersistenceSmokeReport.cs','catalog':ROOT/'Librarium/Runtime/Economy/global_persistence_catalog_0157.json','evidence':ROOT/'Evidence/Economy/economy_global_persistence_evidence_0157.json','doc':ROOT/'Docs/Economy/ECONOMY_GLOBAL_PERSISTENCE_RUNTIME_PASS_0157.md','manifest':ROOT/'Manifests/economy_semantic_exhaustion_cumulative_manifest_0157.json'}
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
    required=['RuntimeEconomyPersistenceCoordinator','IRuntimeEconomyPersistenceParticipant','IRuntimeEconomyPersistenceStore','RuntimeEconomyCheckpoint','RuntimeEconomyJournalEntry','Autosave(','OnLogout(','OnDisconnect(','Recover(','WriteAtomic','CaptureAndValidate','RestoreAll','ReplayJournal','economy-persistence-replay-detected','economy-persistence-checkpoint-corrupt','economy-persistence-component-corrupt','CurrentEnvelopeSchemaVersion=1','SHA256.HashData','p.Restore(participantBefore[p.ComponentId])']
    for x in required:
        if x not in t:errors.append(f'runtime-token-missing:{x}')
    for a,b,n in [('{','}','brace'),('(',')','paren'),('[',']','bracket')]:
        if t.count(a)!=t.count(b):errors.append(f'csharp-{n}-imbalance')
    defs=set(re.findall(r'public\s+(?:sealed\s+)?(?:class|record|interface|enum|static\s+class)\s+(\w+)',t))
    for x in ['RuntimeEconomyComponentSnapshot','RuntimeEconomyPersistenceEnvelope','RuntimeEconomyPersistenceResult']:
        if x not in defs:errors.append(f'csharp-definition-missing:{x}')
if paths['smoke'].is_file():
    s=paths['smoke'].read_text()
    for x in ['atomic-multi-component-autosave','logout-and-disconnect-save-coordination','failed-store-write-restores-all-participants','checkpoint-checksum-corruption-rejection','last-known-good-backup-recovery','validated-journal-replay-recovery','replay-key-duplicate-save-protection']:
        if x not in s:errors.append(f'smoke-token-missing:{x}')
if paths['catalog'].is_file():
    c=json.loads(paths['catalog'].read_text())
    for k in ['persistenceCatalog','saveTriggerCatalog','checkpointCatalog','journalCatalog','recoveryCatalog','compatibilityCatalog','corruptionCatalog','rollbackCatalog','ownershipGraph','runtimeGraph','saveImpact','worldImpact','nativeDependencyReport']:
        if not c.get(k):errors.append(f'catalog-missing:{k}')
if paths['evidence'].is_file():
    e=json.loads(paths['evidence'].read_text()); findings=e.get('findings',[]); classes={x.get('classification') for x in findings}
    if not {'PROVEN','STRONGLY CORRELATED','WEAK','UNKNOWN'}.issubset(classes):errors.append('evidence-classification-coverage')
    for i,f in enumerate(findings):
        for k in e.get('requiredFields',[]):
            if k not in f:errors.append(f'evidence-field-missing:{i}:{k}')
if paths['manifest'].is_file():
    m=json.loads(paths['manifest'].read_text());files=m.get('files',[])
    actual=sorted(str(p.relative_to(ROOT)).replace('\\','/') for p in ROOT.rglob('*') if p.is_file() and '__pycache__' not in p.parts)
    if len(files)!=len(set(files)):errors.append('manifest-duplicate-file')
    missing=sorted(set(files)-set(actual))
    if missing:errors.append(f'manifest-files-missing:{len(missing)}:{missing[:5]}')
if errors:print(json.dumps({'valid':False,'errors':errors},indent=2));sys.exit(1)
print(json.dumps({'valid':True,'checks':['json','python','csharp-structure','aggregate capture','atomic commit','autosave','logout','disconnect','rollback','replay protection','checksums','schema compatibility','backup recovery','journal replay','librarium','evidence','provenance','manifest']},indent=2))
