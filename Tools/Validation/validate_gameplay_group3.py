import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else r'E:\MajorMud-Reforged Github')
errors=[]
counts={}
for family in ('shops','banks','economy_rules'):
 p=root/'Data'/'Classic'/f'{family}.json'
 if not p.exists(): errors.append(f'missing:{p}'); continue
 d=json.loads(p.read_text(encoding='utf-8'))
 records=d.get('records',[])
 counts[family]=len(records)
 if d.get('record_count') != len(records): errors.append(f'record-count-mismatch:{family}')
 if not records: errors.append(f'empty:{family}')
required={
 'Apps/Source/Reforged.Server/RuntimeServicesEconomy.cs':['RuntimeServiceCatalog','RuntimeCurrencyAccount','RuntimeServiceTransactionEngine','RuntimeTradeSession','RuntimeServicesEconomyValidator','IRuntimeTrainerServicePolicy'],
 'Apps/Source/Reforged.Server/RuntimeServicesEconomySmokeReport.cs':['RuntimeServicesEconomySmokeReport','trainer-contract','trade-confirmation','rollback'],
 'Apps/Source/Reforged.Server/Program.cs':['--smoke-services-economy']}
for rel,tokens in required.items():
 p=root/rel
 if not p.exists(): errors.append(f'missing:{rel}'); continue
 text=p.read_text(encoding='utf-8')
 for token in tokens:
  if token not in text: errors.append(f'missing-token:{rel}:{token}')
print(json.dumps({'passed':not errors,'shop_count':counts.get('shops',0),'bank_count':counts.get('banks',0),'economy_rule_count':counts.get('economy_rules',0),'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
