from pathlib import Path
import json, sys
root=Path(__file__).resolve().parents[2]
errors=[]
required={
 'Apps/Source/Reforged.Server/RuntimeGameplayCompletionState.cs':['RuntimeGameplayCompletionStateStore','normal_qty','regen_qty','regen_time','ExecuteShopTransaction'],
 'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs':['ExecuteCharacterTransaction'],
 'Apps/Source/Reforged.Server/RuntimeLiveGameplayPart2.cs':['ExecuteCharacterTransaction','You cannot give an item to yourself'],
 'Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs':['out of stock','ExecuteShopTransaction','generated-shop-restock-contract'],
}
for rel, markers in required.items():
 p=root/rel
 if not p.exists(): errors.append('missing:'+rel); continue
 text=p.read_text(encoding='utf-8')
 for marker in markers:
  if marker not in text: errors.append(f'missing-marker:{rel}:{marker}')
shops=root/'Data/Classic/shops.json'
if not shops.exists(): errors.append('missing-generated-shops')
else:
 data=json.loads(shops.read_text(encoding='utf-8'))
 if data.get('authority')!='Librarium': errors.append('shops-not-librarium-authoritative')
 entries=[e for r in data.get('records',[]) for e in r.get('inventory',[]) if e.get('item_exists') is True]
 if not entries: errors.append('no-executable-shop-stock')
 for key in ('normal_qty','max_qty','regen_qty','regen_time','regen_percent'):
  if not any(key in e for e in entries): errors.append('missing-generated-stock-field:'+key)
if errors:
 print('\n'.join('FAIL '+e for e in errors)); sys.exit(1)
print('Gameplay Completion Bundle 4 structure PASS')
