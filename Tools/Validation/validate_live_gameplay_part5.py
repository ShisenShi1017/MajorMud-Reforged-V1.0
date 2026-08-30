from pathlib import Path
import json, sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(r'E:\MajorMud-Reforged Github')
errors=[]
server=root/'Apps/Source/Reforged.Server'
checks={
 'RuntimeLiveGameplayPart5.cs':['ExecuteBankCommand','ResolveBankAtRoom','RuntimeBundleCEconomyAuthority.Deposit','RuntimeBundleCEconomyAuthority.Withdraw','RuntimeLiveGameplayPart5SmokeReport'],
 'RuntimeLiveGameplayPart2.cs':['ExecuteBankCommand(session, command)'],
 'Program.cs':['--smoke-live-gameplay-part5','RuntimeLiveGameplayPart5SmokeReport.Run'],
}
for file,tokens in checks.items():
 p=server/file
 if not p.exists(): errors.append(f'missing:{p}'); continue
 text=p.read_text(encoding='utf-8')
 for token in tokens:
  if token not in text: errors.append(f'{file}:missing-token:{token}')
 if file == 'RuntimeLiveGameplayPart5.cs' and 'RuntimeCurrencyAccount' in text:
  errors.append('RuntimeLiveGameplayPart5.cs:retired-gold-only-bank-bypass')
banks=root/'Data/Classic/banks.json'
if not banks.exists(): errors.append('missing:Data/Classic/banks.json')
else:
 try:
  data=json.loads(banks.read_text(encoding='utf-8'))
  records=data.get('records',[])
  if not records: errors.append('banks-empty')
  if not any('room_number' in r for r in records): errors.append('banks-missing-room-number')
 except Exception as ex: errors.append(f'banks-invalid:{ex}')
print(json.dumps({'passed':not errors,'errors':errors},indent=2))
sys.exit(0 if not errors else 1)
