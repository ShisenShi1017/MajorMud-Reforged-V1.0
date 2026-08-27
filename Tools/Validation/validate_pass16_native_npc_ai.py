#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def req(cond,name):
    checks.append(name)
    if not cond: failures.append(name)
def text(rel): return (root/rel).read_text(encoding='utf-8')

npcs=json.loads(text('Librarium/Objects/npcs/npc_records_0122.json'))['records']
messages=json.loads(text('Librarium/Objects/native_message_payloads/native_message_payload_records.json'))['records']
ledger=json.loads(text('Librarium/Parity/majormud_parity_assertions_0286.json'))
score=json.loads(text('Reports/RuntimeVerification/majormud_parity_scorecard_0286.json'))
contract=text('Apps/Source/Reforged.Server/RuntimeClassicNpcNativeFieldContract.cs')
ownership=text('Apps/Source/Reforged.Server/RuntimeLiveCombatOwnership.cs')
authority=text('Apps/Source/Reforged.Server/RuntimeClassicFormulaAuthority.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')

req(len(npcs)==1100,'classic-npc-count-1100')
follow=[int((r.get('behavior') or {}).get('follow',0) or 0) for r in npcs]
req(min(follow)==0 and max(follow)==100 and all(0<=v<=100 for v in follow),'follow-percent-corpus-0-100')
req(len(set(follow))==24,'follow-percent-distinct-values-24')

# Native byte attack-percent preservation: do not clamp >100 rows.
exec_rows=[]
for r in npcs:
    cnt=int((r.get('combat') or {}).get('attack_count',0) or 0)
    rows=[]
    for a in r.get('attacks') or []:
        fields=['minimum_damage','maximum_damage','selection_percent','accuracy','hit_spell_legacy_id','energy_cost','hit_message_legacy_id','miss_message_legacy_id','dodge_message_legacy_id']
        if int(a.get('slot',0) or 0)>0 and any(int(a.get(k,0) or 0)!=0 for k in fields): rows.append(a)
    exec_rows += sorted(rows,key=lambda a:int(a.get('slot',0) or 0))[:cnt]
req(all(0<=int(a.get('selection_percent',0) or 0)<=255 for a in exec_rows),'attack-percent-native-byte-range')
req(sorted({int(a.get('selection_percent',0) or 0) for a in exec_rows if int(a.get('selection_percent',0) or 0)>100})==[200,232],'attack-percent-over-100-values-preserved-200-232')

# Ability 146 MonsGuards relationship projected from public evidence.
ids={int(r['legacy_id']) for r in npcs}
owners=[]; guard_targets=[]
for r in npcs:
    abilities=(((r.get('public_reference_0139') or {}).get('fields') or {}).get('abilities_named') or [])
    vals=[int(a.get('value',0) or 0) for a in abilities if int(a.get('ability_id',0) or 0)==146 and int(a.get('value',0) or 0)>0]
    if vals: owners.append((int(r['legacy_id']),vals)); guard_targets += vals
req(len(owners)==112,'monsguards-owner-count-112')
req(len(guard_targets)==159,'monsguards-relationship-count-159')
req(all(v in ids for v in guard_targets),'monsguards-all-targets-resolve')

# 8480 is combat presentation, not assist.
payload=next(r for r in messages if int(r.get('message_id',0))==8480)
req(payload.get('player_payload')=='%s %s %s with his %s, but %s dodges!','message-8480-native-dodge-template')
rows8480=[]
for r in npcs:
    pub=(((r.get('public_reference_0139') or {}).get('fields') or {}).get('attacks') or [])
    authored=r.get('attacks') or []
    for i,a in enumerate(authored):
        if int(a.get('miss_message_legacy_id',0) or 0)==8480:
            pa=pub[i] if i < len(pub) else {}
            rows8480.append((int(r['legacy_id']), bool(str(pa.get('miss_verb3','')).strip() and str(pa.get('weapon_name','')).strip())))
req(len(rows8480)==9,'message-8480-nine-rows')
req(sum(bound for _,bound in rows8480)==4,'message-8480-four-bound')
req(all(npc==59 for npc,bound in rows8480 if bound),'message-8480-bound-rows-are-thrag')
req(all(npc==1006 for npc,bound in rows8480 if not bound) and sum(not b for _,b in rows8480)==5,'message-8480-five-unbound-nahr')

# Source wiring / Classic isolation.
for phrase in ['SignedInt16Minimum = -32768','SignedInt16Maximum = 32767','UnsignedByteMaximum = 255','NPC_NATIVE_FOLLOW_PERCENT_RANGE','RuntimeClassicNpcNativeFieldContract.Validate(definition)']:
    req(phrase in contract or phrase in text('Apps/Source/Reforged.Server/RuntimeNpcModels.cs'),f'native-width-contract:{phrase}')
req('var classicWorld = session.World.Definition.Type.Equals("Classic"' in ownership,'classic-guard-world-gate')
req('candidate.Metadata.TryGetValue("guarded_npc_legacy_ids"' in ownership,'classic-monsguards-consumer')
req('if (!classicWorld && !explicitlyLinked)' in ownership,'reforged-assist-links-nonclassic-only')
req('classic.ability-146-monsguards' in ownership,'classic-monsguards-authority-label')
req('Ability 146 is MonsGuards' in authority,'formula-authority-monsguards-evidence')
req('RuntimeClassicNpcAiNativeClosureAudit.Run(root)' in audit,'unified-parity-audit-runs-pass16-npc-audit')
req('majormud_parity_assertions_0286.json' in audit or 'majormud_parity_assertions_0287.json' in audit or 'majormud_parity_assertions_0288.json' in audit,'runtime-parity-ledger-pointer-0286-or-newer')

# Parity ledger stays conservative.
byid={a['id']:a for a in ledger['assertions']}
req(ledger.get('update')=='0286','ledger-update-0286')
req(byid.get('npc.native-monster-record-width-contract',{}).get('state')=='PASS','ledger-native-width-pass')
req(byid.get('npc.monsguards-relationship',{}).get('state')=='STRONG','ledger-monsguards-strong')
req(byid.get('npc.native-ai-semantics',{}).get('state')=='UNKNOWN','ledger-native-ai-remains-unknown')
req(byid.get('npc.native-combat-message-8480',{}).get('state')=='UNKNOWN','ledger-8480-remains-unknown')
req(score.get('PassCount')==72,'score-pass-72')
req(score.get('StrongCount')==59,'score-strong-59')
req(score.get('UnknownCount')==47,'score-unknown-47')
req(score.get('ScoredRequirementCount')==178,'score-total-178')
req(abs(score.get('StrictPercent',0)-40.4)<0.01,'score-strict-40.4')
req(abs(score.get('EvidenceBackedPercent',0)-73.6)<0.01,'score-evidence-73.6')

report={'schema':'majormud.pass16-native-npc-ai-validation.v1','update':'0286','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/native_npc_ai_pass16_static_validation_0286.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
sys.exit(1 if failures else 0)
