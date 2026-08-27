#!/usr/bin/env python3
import json, sys
from collections import Counter
from pathlib import Path
root=Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def check(ok,name,detail=''):
    (checks if ok else failures).append(name if not detail else f'{name}: {detail}')
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))

ledger=js('Librarium/Parity/majormud_parity_assertions_0293.json')
counts=Counter(a['state'] for a in ledger['assertions'])
check(len(ledger['assertions'])==179,'ledger-total')
check(counts==Counter({'PASS':73,'STRONG':75,'UNKNOWN':31}),'ledger-score',str(counts))
for aid in ['economy.non-item-service-cost-order','native.paid-room-transfers']:
    a=next(x for x in ledger['assertions'] if x['id']==aid)
    check(a['state']=='STRONG','promoted-strong:'+aid)
    check('0293' in ' '.join(a.get('evidence',[])),'0293-evidence:'+aid)

# Native healer command and service proof.
helptext=text('Librarium/Evidence/native_message_catalog_correlation_0022.json').lower()
check('buy healing' in helptext,'native-buy-healing-help')
check('heal all your wounds for a price' in helptext,'native-heal-all-wounds-help')
messages=js('Librarium/Objects/native_message_payloads/native_message_payload_records.json')['records']
msg318=next(x for x in messages if x['message_id']==318)
check(msg318['player_payload']=='You cannot afford that service.','native-message-318-exact')

# Decode exact healer family and immediate room relocation subset.
rows=js('Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142/textblocks_decoded.json')
healer_lines=[]; relocation_lines=[]
for row in rows:
    if row.get('owner',{}).get('kind')!='room': continue
    for trigger in row.get('triggers',[]):
        ops=sorted([op for op in row.get('operations',[]) if op.get('line')==trigger.get('line')], key=lambda x:x.get('sequence',0))
        codes=[op.get('opcode') for op in ops]
        if codes==['monsters','price','cast','adddelay']:
            cast=next(op for op in ops if op.get('opcode')=='cast')
            sid=cast.get('spell_id') or (cast.get('numeric_arguments') or [0])[0]
            if 219<=sid<=224: healer_lines.append((row['textblock_id'],trigger['raw'],ops))
        if codes and codes.count('teleport')==1 and all(code in ('message','text','teleport') for code in codes):
            relocation_lines.append((row['textblock_id'],trigger['raw'],ops))
check(len(healer_lines)==132,'decoded-healer-line-count',str(len(healer_lines)))
check(sorted(set(x[0] for x in healer_lines))==[159,1602,1603,1604,1605,1606,1608,1609,1610,1611,4351],'decoded-healer-textblocks')
check(len(relocation_lines)==45,'simple-relocation-line-count',str(len(relocation_lines)))
check(sorted(set(x[0] for x in relocation_lines))==[307,310,1052,2931,2932,2935,2936,4087,9047,9539,9552,9554,9556],'simple-relocation-textblocks')

# Representative healer prices and spell identities.
def line(tb,trigger): return next(ops for tid,trig,ops in healer_lines if tid==tb and trig==trigger)
for tb,trig,price,sid in [(159,'buy minor healing',50,219),(159,'buy major healing',120,220),(159,'buy greater healing',200,221),(159,'buy cure poison',1000,222),(159,'buy cure disease',2500,223),(159,'buy remove curse',5000,224),(4351,'buy minor healing',100,219),(4351,'buy remove curse',10000,224)]:
    ops=line(tb,trig); p=next(x for x in ops if x['opcode']=='price'); c=next(x for x in ops if x['opcode']=='cast')
    check(p['numeric_arguments']==[price,318],f'healer-price:{tb}:{trig}')
    check((c.get('spell_id') or c['numeric_arguments'][0])==sid,f'healer-spell:{tb}:{trig}')

spells=js('Librarium/Objects/spells/spell_records_0122.json')['records']
names={int(r['legacy_id']):r['name'] for r in spells if r.get('legacy_id','').isdigit() and 219<=int(r['legacy_id'])<=224}
check(names=={219:'minor healing',220:'major healing',221:'greater healing',222:'cure poison',223:'cure disease',224:'remove curse'},'healer-spell-identities',str(names))

# Runtime room graph + execution hook.
graph=text('Apps/Source/Reforged.Server/RuntimeClassicTextblockGraphAuthority.cs')
check('TryResolveRoomActionPlan' in graph,'room-action-plan-resolver')
check('OwnerKind.Equals("room"' in graph,'room-action-owner-boundary')
check('ContentKind.Equals("action_script"' in graph,'room-action-kind-boundary')
check('RoomOwnerContains' in graph,'room-owner-map-room-match')

runtime=text('Apps/Source/Reforged.Server/RuntimeClassicRoomActionScripts.cs')
for token,name in [
    ('IsRecoveredSimpleHealerPlan','simple-healer-gate'),('monsters','healer-presence-condition'),
    ('RuntimeBundleCInventoryAuthority.ReadCurrency','healer-affordability-before-effect'),
    ('RuntimeBundleCEconomyAuthority.DebitCopperValue','healer-debit-authority'),
    ('RequireMessage(insufficientFundsMessageId)','native-healer-failure-message'),
    ('ApplyDecodedCureAndDispel','cure-authority-reused'),('ApplyDecodedRemoveCurse','remove-curse-authority-reused'),
    ('RuntimeClassicPlayerSpellCombat.Resolve','healing-spell-resolver-reused'),
    ('classic-healer-rollback','healer-rollback-path'),('PersistLivePowers','live-power-persistence'),
    ('IsRecoveredImmediateRelocationPlan','simple-relocation-gate'),('RequireMessage(messageId)','native-relocation-message'),
    ('RequireTextblock(textblockId)','native-relocation-text'),('FindRoom(mapNumber, roomNumber)','decoded-map-room-destination'),
    ('MoveToWorld','immediate-room-move'),('_characters.SaveSession(session)','relocation-save'),
    ('AdmitClassicRoomEntryNpcRespawns(session)','room-entry-respawn-admission')]: check(token in runtime,name)
check('adddelay' in runtime and 'opcodes[3].Equals("adddelay"' in runtime,'decoded-healer-delay-recognized')
check('AddSeconds' not in runtime and 'TimeSpan.From' not in runtime,'adddelay-unit-not-invented')
check('vehicle' not in runtime.lower() and 'boarding' not in runtime.lower(),'no-transport-state-in-room-actions')

living=text('Apps/Source/Reforged.Server/RuntimeLivingWorld.cs')
check('TryExecuteClassicRoomActionScript(session, command, out result)' in living,'room-action-hook-live')
casino_index=living.find('TryExecuteClassicCasinoCommand(session, command, out result)')
action_index=living.find('TryExecuteClassicRoomActionScript(session, command, out result)')
objects_index=living.find('CaptureWorldObjectDefinitions()', action_index)
check(casino_index>=0 and action_index>casino_index and objects_index>action_index,'room-action-dispatch-order')

# Ticket/pass gate remains possession-only until consumption proof.
gates=text('Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs')
check('case "ticket_item"' in gates,'ticket-item-gate-live')
check('Ticket/Item prerequisite' in gates,'ticket-native-type17-comment')
check('do not invent ticket destruction' in gates,'ticket-consumption-gated')

# Native relocation representative rows.
def has_relocation(tb,trig,room,mapn):
    for tid,t,ops in relocation_lines:
        if tid==tb and t==trig:
            tele=next(x for x in ops if x['opcode']=='teleport')
            return tele.get('room_number')==room and tele.get('map_number')==mapn
    return False
for args,name in [((307,'go hole',487,2),'hole-2-1306-to-2-487'),((1052,'go opening',1359,6),'opening-6-1354-to-6-1359'),((2931,'read book',740,15),'book-10-235-to-15-740'),((2932,'touch crystal',773,15),'crystal-15-772-to-15-773'),((4087,'ring chime',65,1),'chime-room-relocation'),((9552,'love',1743,17),'love-room-relocation')]:
    check(has_relocation(*args),name)

# Payload fallback is required in source-only parity/audit workspaces.
payload_auth=text('Apps/Source/Reforged.Server/RuntimeClassicNativeTextblockPayloadAuthority.cs')
check('native_textblock_payload_records.json' in payload_auth,'textblock-payload-librarium-fallback')
check('native_message_payload_records.json' in payload_auth,'message-payload-librarium-fallback')
check('Expected 3467 WCCTEXT2 payloads' in payload_auth,'textblock-payload-count-guard')
check('Expected 3869 logical WCCMSG2 payloads' in payload_auth,'message-payload-count-guard')

registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
prs=next(x for x in registry['systems'] if x['id']=='paid-room-transfers')
check(prs['classification']=='runtime-active-strong-evidence','registry-access-relocation-strong')
check('no transport persistence exists' in prs['persistenceOwner'],'registry-no-transport-persistence')
check(any('consumed' in x for x in prs['nativeDependencies']),'registry-ticket-consumption-still-open')

score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0293.json')
check((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(73,75,31,179),'scorecard-counts')
check(score['StrictPercent']==40.8,'scorecard-strict')
check(score['EvidenceBackedPercent']==82.7,'scorecard-backed')
econ=next(x for x in score['Categories'] if x['Category']=='Economy & Services')
check((econ['Pass'],econ['Strong'],econ['Unknown'])==(1,15,0),'economy-category-closure')

audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
check('majormud_parity_assertions_0293.json' in audit,'runtime-audit-ledger-pointer')
check('majormud_ownership_scope_0291.json' in audit,'runtime-audit-scope-preserved')

server=text('Apps/Source/Reforged.Server/ServerRuntimeHost.cs')
check('public sealed partial class ServerRuntimeHost' in server,'borin-partial-host-preserved')
check('RuntimeClassicTimingAuthority.PlayerDataSaveInterval' in server,'classic-60-second-save-preserved')
check('TimeSpan.FromMinutes(5)' not in server,'trainer-autosave-regression-not-reintroduced')
check((root/'Librarium/WorldOverrides/Custom1/overrides.json').exists(),'custom1-editor-update-preserved')
check((root/'AI/Training/borin_headless_trainer.py').exists() and (root/'Apps/Source/Reforged.Server/RuntimeHeadlessTraining.cs').exists(),'borin-trainer-files-preserved')

for rel in ['Librarium/Parity/majormud_parity_assertions_0293.json','Librarium/Parity/majormud_healers_access_room_commands_evidence_0293.json','Librarium/RemainingSystems/remaining_systems_registry.json','Reports/RuntimeVerification/majormud_parity_scorecard_0293.json']:
    try: js(rel); check(True,'json:'+rel)
    except Exception as e: check(False,'json:'+rel,str(e))

for rel in ['Apps/Source/Reforged.Server/RuntimeClassicRoomActionScripts.cs','Apps/Source/Reforged.Server/RuntimeLivingWorld.cs','Apps/Source/Reforged.Server/RuntimeClassicTextblockGraphAuthority.cs','Apps/Source/Reforged.Server/RuntimeClassicNativeTextblockPayloadAuthority.cs','Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs']:
    s=text(rel); check(s.count('{')==s.count('}'),'brace-balance:'+rel,f'{s.count("{")} vs {s.count("}")}')

report={'Passed':not failures,'CheckCount':len(checks)+len(failures),'PassedCount':len(checks),'FailureCount':len(failures),'Checks':checks,'Failures':failures}
out=root/'Reports/RuntimeVerification/healers_access_room_commands_pass23_static_validation_0293.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
sys.exit(0 if not failures else 1)
