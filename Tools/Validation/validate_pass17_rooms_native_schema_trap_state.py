import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
checks=[]; failures=[]
def req(c,n): (checks if c else failures).append(n)
def text(rel): return (root/rel).read_text(encoding='utf-8')
def load(rel): return json.loads(text(rel))

contract=text('Apps/Source/Reforged.Server/RuntimeClassicRoomExitNativeFieldContract.cs')
trap=text('Apps/Source/Reforged.Server/RuntimeClassicExitTraps.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeClassicRoomsDoorsTrapsParityAudit.cs')
evidence=load('Librarium/Parity/majormud_room_exit_native_schema_evidence_0287.json')
ledger=load('Librarium/Parity/majormud_parity_assertions_0287.json')
score=load('Reports/RuntimeVerification/majormud_parity_scorecard_0287.json')
parity=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')

for token in ['RoomExitType : short','TrapState : short','HiddenPassable = 1','HiddenNeedsSearch = 2','BlockedTenActions = 16368','HiddenOnce = 16384','PermanentVisible = 32768']:
 req(token in contract,'contract:'+token)
req(evidence['native_room_exit_storage']['RoomType']=='signed 16-bit Integer','room-type-int16')
req(evidence['native_room_exit_storage']['Para2']=='signed 16-bit Integer','para2-int16')
req(evidence['native_room_exit_storage']['Para1']=='signed 32-bit Long','para1-int32')
req(evidence['trap_schema']['current_classic_catalog']=={'total':295,'damage':273,'spell':22,'native_activation_state_projected':0},'trap-catalog-and-zero-state-projection')
req('NativeActivationStateProjected' in trap,'trap-record-keeps-projection-status')
req('if (!trap.NativeActivationStateProjected) return string.Empty;' in trap,'trap-unknown-activation-fails-closed')
req('do not guess that every decoded trap is Active' in trap,'trap-no-always-active-assumption')
req('majormud_room_exit_native_schema_evidence_0287.json' in audit,'rooms-audit-connects-0287-evidence')
req('majormud_parity_assertions_0287.json' in parity or 'majormud_parity_assertions_0288.json' in parity,'unified-parity-points-0287')
by={a['id']:a for a in ledger['assertions']}
expected={
 'traps.classic-exit-catalog':'PASS',
 'traps.damage-exit-runtime':'STRONG',
 'traps.spell-exit-runtime':'STRONG',
 'traps.live-movement-order':'STRONG',
 'traps.native-activation-state-schema':'STRONG',
 'rooms.native-exit-storage-schema':'STRONG',
 'native.traps':'UNKNOWN',
 'native.doors-locks-access':'UNKNOWN',
 'rooms.unknown-qualified-exits':'UNKNOWN',
}
for k,v in expected.items(): req(by.get(k,{}).get('state')==v,f'ledger:{k}:{v}')
counts={s:sum(1 for a in ledger['assertions'] if a.get('state')==s) for s in ('PASS','STRONG','UNKNOWN')}
req(counts=={'PASS':70,'STRONG':63,'UNKNOWN':47},f'ledger-counts:{counts}')
req(score['PassCount']==70 and score['StrongCount']==63 and score['UnknownCount']==47 and score['ScoredRequirementCount']==180,'score-counts')
req(score['StrictPercent']==38.9 and score['EvidenceBackedPercent']==73.9,'score-percentages')

report={'schema':'majormud.pass17-rooms-native-schema-trap-state-validation.v1','update':'0287','status':'PASS' if not failures else 'FAIL','checks':len(checks)+len(failures),'failures':failures}
out=root/'Reports/RuntimeVerification/rooms_native_schema_trap_state_pass17_static_validation_0287.json'; out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2)); sys.exit(0 if not failures else 1)
