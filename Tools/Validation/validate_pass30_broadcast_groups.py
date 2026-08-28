#!/usr/bin/env python3
import json, pathlib, re
from collections import Counter
root=pathlib.Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def req(cond,name,detail=''):
    checks.append(name)
    if not cond: failures.append(name + (f': {detail}' if detail else ''))
def text(rel): return (root/rel).read_text(encoding='utf-8')
def js(rel): return json.loads(text(rel))
def all_strings(v):
    if isinstance(v,dict):
        for x in v.values(): yield from all_strings(x)
    elif isinstance(v,list):
        for x in v: yield from all_strings(x)
    elif isinstance(v,str): yield v

messages=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
corpus='\n'.join(all_strings(messages))
evidence=js('Librarium/Parity/majormud_broadcast_group_evidence_0301.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0301.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0301.json')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
authority=text('Apps/Source/Reforged.Server/RuntimeClassicBroadcastGroupAuthority.cs')
party=text('Apps/Source/Reforged.Server/RuntimeClassicPartyGameplay.cs')
live=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
cmd=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
routes=text('Apps/Source/Reforged.Server/RuntimeCommandRouting.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass30BroadcastGroupSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')

# Immutable native help statements and hashes.
for phrase,name in [
    ('JOIN <n> where n is a number between 1 and 127','help123-1-127'),
    ('BROADCAST (or BR) by itself will give you a list','help123-bare-list'),
    ('between 1 and 126','help157-1-126'),
    ('JOIN 0 to go back to the default','help157-join0'),
    ('switch to group 0 (no group)','help217-leave-group0')]:
    req(phrase in corpus,name)
req(evidence['native_help']['message_123']['sha256']=='de65a7eacbb6604d768f976f46df26fc67e5cd9270ced81bb1fab8fe894c2f42','help123-sha')
req(evidence['native_help']['message_157']['sha256']=='b8ad1ce4712e2edf7ca22d322ed8449f7beac97420e945c69f3c3968fe69a49b','help157-sha')
req(evidence['native_help']['message_217']['sha256']=='fb2c7a9e2eb1cca7b6873c1641b75accb35077f8f7e5f897f7f9460519a780d9','help217-sha')
req(evidence['promoted_exact_intersection']['selectable_groups']=='0..126','intersection-0-126')
req('conflict' in evidence['remaining_unknown']['group_127'].lower(),'group127-stays-conflicted')

# Runtime boundary and routing.
for token,name in [
    ('DefaultGroup = 0','authority-default0'),
    ('HighestUncontestedGroup = 126','authority-high126'),
    ('ConflictedUpperGroup = 127','authority-conflicted127'),
    ('RuntimeClassicBroadcastGroupSelectionState.EvidenceConflict','authority-conflict-state')]: req(token in authority,name)
req('return SetClassicBroadcastGroup(session, broadcastGroup);' in party,'join-numeric-selects-group')
req('if (party is null) return SetClassicBroadcastGroup(session, 0);' in party,'leave-no-party-resets0')
req('public string Broadcast(RuntimeSession session, string text)' in live,'broadcast-runtime-present')
req('Native BROADCAST behavior while in default group 0 is not separately recovered.' in live,'broadcast-group0-fails-closed')
req('ReadClassicBroadcastGroup(targetRecord) == group' in live,'broadcast-membership-by-group')
req('currentMembers.Select(target => target.Actor.DisplayName)' in live,'bare-broadcast-lists-members')
req('target.EnqueueMessage($"[Broadcast {group}]' in live,'broadcast-delivery-active')
req('ClassicBroadcastGroupKey = "communication.broadcast.group"' in live,'broadcast-selection-persisted')
req('"broadcast" => _gameplay?.Broadcast(session, command.ArgumentText)' in cmd,'dispatcher-broadcast-live')
req('new("broadcast", ["br"], 9)' in cmd,'br-native-alias-preserved')
comm_slice=routes[routes.index('RuntimeCommandRouteKind.CommunicationRuntime'):routes.index('RuntimeCommandRouteKind.MajorMudPlusRuntime')]
gate_slice=routes[routes.index('RuntimeCommandRouteKind.NativeEvidenceGate'):routes.index('RuntimeCommandRouteKind.RuntimeDiagnostics')]
req('"broadcast"' in comm_slice,'broadcast-communication-route')
req('"broadcast"' not in gate_slice,'broadcast-removed-identity-only-gate')

# Executable smoke surface.
for token in ['broadcast-group-0-selectable','broadcast-group-126-selectable','broadcast-group-127-remains-evidence-conflict','br-alias-resolves-broadcast','broadcast-routes-to-communication-runtime','join-numeric-route-present','leave-route-present']:
    req(token in smoke,f'smoke:{token}')
req('--smoke-pass30-broadcast-groups' in program,'smoke-cli')

# Registry and unresolved boundaries.
systems={s['id']:s for s in registry['systems']}
party_reg=systems['party-coordination']
req('broadcastGroup0301' in party_reg,'registry-broadcast0301')
req('group127' in party_reg['broadcastGroup0301'] and party_reg['broadcastGroup0301']['group127'].startswith('UNKNOWN'),'registry-127-unknown')
req(any('native persistence ownership' in x for x in party_reg['nativeDependencies']),'registry-persistence-rendering-gated')

# Ledger closure is deliberately narrow: two new PASS assertions, broad UNKNOWNs stay.
byid={a['id']:a for a in ledger['assertions']}; counts=Counter(a['state'] for a in ledger['assertions'])
req(ledger.get('update')=='0301','ledger-update-0301')
req(len(ledger['assertions'])==182,'ledger-total-182')
req(counts==Counter({'PASS':78,'STRONG':76,'UNKNOWN':28}),'ledger-counts',str(counts))
req(byid.get('native.broadcast-group-selection-reset',{}).get('state')=='PASS','ledger-broadcast-selection-pass')
req(byid.get('native.broadcast-group-send-list',{}).get('state')=='PASS','ledger-broadcast-send-list-pass')
for aid in ['commands.native-player-command-table','native.party-coordination','native.communications.persistence-logging','native.traps']:
    req(byid.get(aid,{}).get('state')=='UNKNOWN',f'parent-unknown:{aid}')
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(78,76,28,182),'score-counts')
req(abs(score['StrictPercent']-42.9)<0.01,'score-strict-42.9')
req(abs(score['EvidenceBackedPercent']-84.6)<0.01,'score-evidence-84.6')
req('majormud_parity_assertions_0316.json' in audit,'runtime-audit-current-ledger')

# Recent compile-regression fixes stay fixed.
bundle=text('Apps/Source/Reforged.Server/RuntimeBundleDRemainingExhaustionAudit.cs')
world=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
app=text('Apps/Source/Reforged.App/App.xaml.cs')
main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req('StrengthAlternative is not null' not in bundle,'strengthalternative-regression-absent')
req(world.count('bool enabled;') <= 1,'enabled-shadow-regression-absent')
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;',app))==1,'wpf-services-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(',app))==1,'wpf-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(',app))==1,'wpf-onexit-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-absent')

report={'schema':'majormud.pass30-broadcast-groups-validation.v1','update':'0301','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/broadcast_groups_pass30_static_validation_0301.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
sys_exit=0 if not failures else 1
raise SystemExit(sys_exit)
