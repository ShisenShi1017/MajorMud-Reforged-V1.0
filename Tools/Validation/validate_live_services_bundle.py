import json, pathlib, sys
root = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else pathlib.Path(r'E:\MajorMud-Reforged Github')
server = root / 'Apps' / 'Source' / 'Reforged.Server'
errors=[]
required=['RuntimeLiveGameplayPart6.cs','RuntimeCommandSystem.cs','RuntimeLiveGameplayPart2.cs','Program.cs']
for name in required:
    if not (server/name).is_file(): errors.append(f'missing:{name}')
part=(server/'RuntimeLiveGameplayPart6.cs').read_text(encoding='utf-8') if (server/'RuntimeLiveGameplayPart6.cs').exists() else ''
for token in ['ExecuteLiveServicesBundleCommand','RenderServicesHere','RenderServiceInventory','RenderTrainerHere','ExecuteDirectTrade','RuntimeLiveGameplayPart6SmokeReport']:
    if token not in part: errors.append(f'missing-token:{token}')
shops=root/'Data'/'Classic'/'shops.json'
if not shops.is_file(): errors.append('missing-generated-shops')
else:
    records=json.loads(shops.read_text(encoding='utf-8')).get('records',[])
    if not any(r.get('service_kind')=='trainer' for r in records): errors.append('missing-generated-trainers')
    if not any(isinstance(r.get('inventory'),list) for r in records): errors.append('missing-generated-shop-inventory')
print(json.dumps({'passed':not errors,'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
