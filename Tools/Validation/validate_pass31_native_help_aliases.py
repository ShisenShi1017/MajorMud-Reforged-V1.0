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

catalog=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
evidence=js('Librarium/Parity/majormud_native_help_main_alias_evidence_0302.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0302.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0302.json')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
help_cs=text('Apps/Source/Reforged.Server/RuntimeMajorMudNativeHelp.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass31NativeHelpAliasesSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
help_catalog=next(c for c in catalog['catalogs'] if c.get('mcv_file','').lower()=='wccmmhlp.mcv')
entries={e['message_number']:e for e in help_catalog['entries'] if e.get('defined')}
def clean_message(n):
    value=entries[n].get('decoded_cp437','')
    value=re.sub(r'\x1b\[[0-9;]*[A-Za-z]','',value).replace('\x00','')
    return value
main=clean_message(1); help_body=clean_message(263)

# Retained native help exact statements.
for phrase,name in [
    ('Laws     - Before thinking of doing anything nasty, read this','main-laws'),
    ('Misc     - Miscellaneous commands','main-misc'),
    ('Set      - Various toggleable profile options','main-set'),
    ('Help     - A quick description of how to use the help system','main-help')]:
    req(phrase in main,name)
req('To get a list of topics available, type HELP TOPICS.' in help_body,'help-topics-advertised')
req('For this help message, type HELP HELP' in help_body,'help-help-syntax')
for n,sha in {
    'message_1':'23d6974b81f16e582a7a0b58fc1df51d7e52c604844dc42887818ddef7064a0b',
    'message_31':'7d521c6d9be015a6a0fca923a67c7d408aa8cbcc34bfe7b58bbe07efc6cfbd93',
    'message_111':'ee5b2c13e3ce02eab71a61c9f4b2105d44c70f49c1f7295c285bae42ea025246',
    'message_117':'17ddee12301b928f4f522b1dd6faf110dc65d574951d5b146c30ebe7e7841bc2',
    'message_263':'990d7d3d9b1ee33b8c0f8d4cada66b31085537a0f2ecf2009eb2ce12faf1dd82'} .items():
    req(evidence['native_help'][n]['sha256']==sha,f'{n}-sha')

# Runtime direct aliases: exact menu labels only.
for token,body in [('"laws" => 31',31),('"misc" => 111',111),('"set" => 117',117),('"help" => 263',263)]:
    req(token in help_cs,f'runtime-alias-body-{body}')
req('"topics" =>' not in help_cs,'topics-not-invented-as-direct-body')
req('var exact = _topics.Where' in help_cs,'conservative-exact-token-resolver-still-present')
req('if (exact.Length == 1' in help_cs,'ambiguity-still-fails-closed')

# Executable smoke surface.
for token in [
    'help-laws-direct-main-topic','help-misc-direct-main-topic','help-set-direct-main-topic',
    'help-help-direct-main-topic','help-topics-rendering-remains-gated','ambiguous-follow-resolver-remains-gated']:
    req(token in smoke,f'smoke:{token}')
req('--smoke-pass31-native-help-aliases' in program,'smoke-cli')
req('RuntimePass31NativeHelpAliasesSmokeReport.Run(projectRoot)' in program,'smoke-project-root')

# Registry and unresolved boundaries.
systems={s['id']:s for s in registry['systems']}
help_reg=systems['help-reference']
req(registry.get('updatedByPass') in {'Pass 31 / Update 0302','Pass 32 / Update 0303','Pass 33 / Update 0304','Pass 34 / Update 0305','Pass 35 / Update 0306','Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313','Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'},'registry-pass31-or-newer')
req('nativeHelpAlias0302' in help_reg,'registry-help0302')
req(help_reg['nativeHelpAlias0302']['state']=='PASS-narrow','registry-pass-narrow')
req(any('HELP TOPICS' in x for x in help_reg['nativeHelpAlias0302']['remainingUnknown']),'registry-topics-unknown')
req(any('ambiguity' in x.lower() for x in help_reg['nativeHelpAlias0302']['remainingUnknown']),'registry-ambiguity-unknown')

# Ledger delta is narrow: four exact PASS assertions; broad native HELP UNKNOWN stays.
byid={a['id']:a for a in ledger['assertions']}; counts=Counter(a['state'] for a in ledger['assertions'])
req(ledger.get('update')=='0302','ledger-update-0302')
req(len(ledger['assertions'])==186,'ledger-total-186')
req(counts==Counter({'PASS':82,'STRONG':76,'UNKNOWN':28}),'ledger-counts',str(counts))
for aid in [
    'commands.native-help-laws-main-alias','commands.native-help-misc-main-alias',
    'commands.native-help-set-main-alias','commands.native-help-help-main-alias']:
    req(byid.get(aid,{}).get('state')=='PASS',f'ledger-pass:{aid}')
req(byid.get('native.help-reference',{}).get('state')=='UNKNOWN','parent-help-reference-stays-unknown')
req(byid.get('commands.native-player-command-table',{}).get('state')=='UNKNOWN','command-table-stays-unknown')
req(byid.get('commands.native-help-topic-bodies',{}).get('state')=='STRONG','topic-bodies-stay-strong')

# Scorecard.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(82,76,28,186),'score-counts')
req(abs(score['StrictPercent']-44.1)<0.01,'score-strict-44.1')
req(abs(score['EvidenceBackedPercent']-84.9)<0.01,'score-evidence-84.9')
cmdcat=next(c for c in score['Categories'] if c['Category']=='Commands & Help')
req((cmdcat['Pass'],cmdcat['Strong'],cmdcat['Unknown'],cmdcat['Total'])==(10,1,2,13),'score-command-help-category')
req('majormud_parity_assertions_0316.json' in audit,'runtime-audit-current-ledger')

# Previous pass/runtime boundaries remain intact.
req('"broadcast" => _gameplay?.Broadcast(session, command.ArgumentText)' in text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs'),'pass30-broadcast-preserved')
req('HighestUncontestedGroup = 126' in text('Apps/Source/Reforged.Server/RuntimeClassicBroadcastGroupAuthority.cs'),'pass30-group126-preserved')
req(('ClassicDefault { get; } = new(2, 3, 60, 1, 18)' in text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')) or ('RuntimeClassicConfigurationAuthority.MaximumEvilPointsForgivenPerDay' in text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')),'pass29-maxepday-preserved')
req('ClassicGivenNameMaximumLength = 10' in text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs'),'pass28-given-name-width-preserved')
req('ClassicFamilyNameMaximumLength = 18' in text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs'),'pass28-family-name-width-preserved')

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

report={'schema':'majormud.pass31-native-help-aliases-validation.v1','update':'0302','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/native_help_aliases_pass31_static_validation_0302.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
