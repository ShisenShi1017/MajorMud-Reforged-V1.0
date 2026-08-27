#!/usr/bin/env python3
import json, re, sys
from collections import Counter
from pathlib import Path

root=Path(__file__).resolve().parents[2]
failures=[]; checks=[]
def req(cond,name,detail=''):
    checks.append(name)
    if not cond: failures.append({'check':name,'detail':detail})
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))

evidence=js('Librarium/Parity/majormud_classic_set_profile_evidence_0303.json')
native=js('Librarium/Evidence/native_message_catalog_correlation_0022.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0303.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0303.json')
registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
world=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
commands=text('Apps/Source/Reforged.Server/RuntimeCommandSystem.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass32ClassicSetProfileSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')

# Exact retained WCCMMHLP records and hashes.
help_catalog=next(c for c in native['catalogs'] if c['mcv_file']=='WCCMMHLP.mcv')
entries={e['message_number']:e for e in help_catalog['entries']}
expected={
 37:'7db8e67af75da2cc1ea286327ef723ff16c2012250decbdc1f137a57c38b9089',
 117:'17ddee12301b928f4f522b1dd6faf110dc65d574951d5b146c30ebe7e7841bc2',
 119:'61c32e8a1decc3a9c4cfb126f035402974aeee69e3e0bff5a01758805a379fb2',
 121:'bc3e8a411086f17bbb80222d6b6c4eb99d5c4c677e76f8bc39f7f07caae09d8d',
 175:'14734ab1a78e32ddf163c3cfb2dd5a3b12c03d4df75a26d21aee87e8ae5256bd',
 177:'98af8e5681bd19241aca1ae896c23f394738a7a84de689789713170cab42f761'}
for n,sha in expected.items(): req(entries[n]['sha256']==sha,f'native-help-{n}-sha')
body117=entries[117]['decoded_cp437']; body119=entries[119]['decoded_cp437']; body121=entries[121]['decoded_cp437']; body175=entries[175]['decoded_cp437']; body177=entries[177]['decoded_cp437']
for phrase in ['Set Messages','Set Follow','Set Entrances','Set Speech','Set Telepath','Set Receive']:
    req(phrase in body117,f'help117:{phrase.lower().replace(" ","-")}')
req('SET SPEECH -- Toggles fast and slow talking on and off' in body119,'help119-speech-bare-toggle')
req('SET FOLLOW (Blind/Normal)' in body119,'help119-follow-values')
req('SET ENTRANCES (On/Off)' in body119,'help119-entrances-values')
req('Low message setting will only display high priority messages' in body121,'help121-low-threshold')
req('Medium message setting will display high and medium' in body121,'help121-medium-threshold')
req('High message setting' in body121 and 'high, medium, and low priority messages' in body121,'help121-high-threshold')
req('too exhaustive to list here' in body121,'help121-classification-not-enumerated')
for phrase in ['SET GOSSIP OFF','SET GOSSIP ON','GOSSIP       (by itself, to toggle it on and off)']:
    req(phrase in body175,f'help175:{phrase.strip().split()[0].lower()}-{len(checks)}')
for phrase in ['SET AUCTION OFF','SET AUCTION ON','AUCTION      (by itself, to toggle it on and off)']:
    req(phrase in body177,f'help177:{phrase.strip().split()[0].lower()}-{len(checks)}')

# Evidence boundary document.
req(evidence.get('update')=='0303','evidence-update-0303')
req(len(evidence.get('nativeHelpEvidence',[]))==6,'evidence-six-native-records')
unresolved=' '.join(evidence.get('remainingUnknown',[])).lower()
for token in ['defaults','telepath','receive','per-message','entrance','maxpages']:
    req(token in unresolved,f'evidence-unknown:{token}')

# Runtime settings are routed and implemented narrowly.
for token in [
 'ClassicMessagesLevelKey = "ui.classic.messages.level"',
 'ClassicEntrancesEnabledKey = "ui.classic.entrances.enabled"',
 'raw.StartsWith("messages", StringComparison.OrdinalIgnoreCase)',
 'raw.StartsWith("entrances", StringComparison.OrdinalIgnoreCase)',
 'ConfigureClassicMessagesLevel(session',
 'ConfigureClassicEntrances(session',
 'ConfigureClassicSpeechSetting(session']:
    req(token in world,f'runtime:{token[:28]}')
req('if (raw.Length == 0) mode = current == "Blind" ? "normal" : "blind";' in world,'set-follow-bare-toggle')
req('else if (raw.Equals("blind", StringComparison.OrdinalIgnoreCase)) mode = "blind";' in world,'set-follow-blind-explicit')
req('else if (raw.Equals("normal", StringComparison.OrdinalIgnoreCase)) mode = "normal";' in world,'set-follow-normal-explicit')
req('raw is not ("low" or "medium" or "high")' in world,'set-messages-only-three-values')
req('[ClassicMessagesLevelKey] = raw' in world,'set-messages-persists-selection')
req(': string.Empty; // Native default remains unresolved; absence is not promoted as a default.' in world,'messages-default-not-invented')
req('bool entrancesEnabled;' in world,'set-entrances-dedicated-local')
req('if (raw.Length == 0) entrancesEnabled = !current;' in world,'set-entrances-bare-toggle')
req('else if (raw.Equals("on", StringComparison.OrdinalIgnoreCase)) entrancesEnabled = true;' in world,'set-entrances-on')
req('else if (raw.Equals("off", StringComparison.OrdinalIgnoreCase)) entrancesEnabled = false;' in world,'set-entrances-off')
req('ReadClassicEntrancesEnabled' in world and 'return null;' in world,'entrances-default-unresolved-reader')
req('WCCMMHLP message 119 explicitly says SET SPEECH toggles Fast/Slow.' in world,'set-speech-native-toggle-comment')
req('mode = currentlyFast ? "slow" : "fast";' in world,'set-speech-bare-toggle')
req('Use: TALK FAST or TALK SLOW.' in world,'bare-talk-not-converted-to-toggle')
req('ConfigureCommunicationFlag(session, ClassicGossipEnabledKey' in world,'set-gossip-handler')
req('ConfigureCommunicationFlag(session, ClassicAuctionEnabledKey' in world,'set-auction-handler')
req('if (raw.Length == 0)' in world and 'toggledEnabled = !CommunicationFlagEnabled' in world,'communication-bare-toggle-helper')
req('Usage: SET {label.ToUpperInvariant()} ON|OFF.' in world,'communication-explicit-on-off-helper')
req('explicit ON/OFF polarity remains evidence-gated' in world,'telepath-explicit-polarity-stays-gated')

# Command usage and smoke path.
for token in ['set messages low|medium|high','set entrances [on|off]','set follow [blind|normal]','set speech [fast|slow]','set gossip [on|off]','set auction [on|off]']:
    req(token in commands,f'command-usage:{token}')
for token in ['native-set-profile-options-present','native-set-messages-thresholds-present','native-gossip-toggle-explicit-forms-present','native-auction-toggle-explicit-forms-present','bare-set-speech-route-present','bare-set-follow-route-present','receive-telepath-evidence-boundary-retained']:
    req(token in smoke,f'smoke:{token}')
req('--smoke-pass32-classic-set-profile' in program,'smoke-cli')
req('RuntimePass32ClassicSetProfileSmokeReport.Run(projectRoot)' in program,'smoke-run')

# Narrow parity delta; broad unresolved communication bucket remains UNKNOWN.
byid={a['id']:a for a in ledger['assertions']}; counts=Counter(a['state'] for a in ledger['assertions'])
req(ledger.get('update')=='0303','ledger-update-0303')
req(len(ledger['assertions'])==192,'ledger-total-192')
req(counts==Counter({'PASS':88,'STRONG':76,'UNKNOWN':28}),'ledger-counts',str(counts))
new_ids=[
 'communications.set-gossip-receipt-contract','communications.set-auction-receipt-contract',
 'settings.set-follow-selection-contract','settings.set-speech-toggle-contract',
 'settings.set-messages-level-contract','settings.set-entrances-selection-contract']
for aid in new_ids: req(byid.get(aid,{}).get('state')=='PASS',f'ledger-pass:{aid}')
req(byid.get('native.communications.persistence-logging',{}).get('state')=='UNKNOWN','broad-communications-stays-unknown')
req(byid.get('commands.talk-fast-slow-mode',{}).get('state')=='STRONG','talk-fast-slow-stays-strong')

# Scorecard and category.
req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(88,76,28,192),'score-counts')
req(abs(score['StrictPercent']-45.8)<0.01,'score-strict-45.8')
req(abs(score['EvidenceBackedPercent']-85.4)<0.01,'score-evidence-85.4')
cat=next(c for c in score['Categories'] if c['Category']=='Parties / Communication / Organizations')
req((cat['Pass'],cat['Strong'],cat['Unknown'],cat['Total'])==(10,3,2,15),'score-communication-category')
req('majormud_parity_assertions_0313.json' in audit,'runtime-audit-current-ledger')

# Registry records the narrow closure and preserves remaining unknowns.
systems={s['id']:s for s in registry['systems']}; comm=systems['communications']
req(registry.get('updatedByPass') in {'Pass 32 / Update 0303','Pass 33 / Update 0304','Pass 34 / Update 0305','Pass 35 / Update 0306','Pass 37 / Update 0308','Pass 38 / Update 0309','Pass 39 / Update 0310','Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313'},'registry-pass32')
req(comm.get('profileSettings0303',{}).get('state')=='PASS-narrow','registry-profile-pass-narrow')
runk=' '.join(comm.get('profileSettings0303',{}).get('remainingUnknown',[])).lower()
for token in ['telepath','receive','per-message','entrance','maxpages']:
    req(token in runk,f'registry-unknown:{token}')

# Previous pass and compile-regression safeguards stay intact.
req('HighestUncontestedGroup = 126' in text('Apps/Source/Reforged.Server/RuntimeClassicBroadcastGroupAuthority.cs'),'pass30-group126-preserved')
req('"laws" => 31' in text('Apps/Source/Reforged.Server/RuntimeMajorMudNativeHelp.cs'),'pass31-laws-alias-preserved')
req('"help" => 263' in text('Apps/Source/Reforged.Server/RuntimeMajorMudNativeHelp.cs'),'pass31-help-alias-preserved')
req(('ClassicDefault { get; } = new(2, 3, 60, 1, 18)' in text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')) or ('RuntimeClassicConfigurationAuthority.MaximumEvilPointsForgivenPerDay' in text('Apps/Source/Reforged.Server/RuntimeCharacterAlignmentAuthority.cs')),'pass29-maxepday-preserved')
req('ClassicGivenNameMaximumLength = 10' in text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs'),'pass28-given-name-preserved')
req('ClassicFamilyNameMaximumLength = 18' in text('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs'),'pass28-family-name-preserved')
bundle=text('Apps/Source/Reforged.Server/RuntimeBundleDRemainingExhaustionAudit.cs')
app=text('Apps/Source/Reforged.App/App.xaml.cs'); main=text('Apps/Source/Reforged.App/MainWindow.xaml.cs')
req('StrengthAlternative is not null' not in bundle,'strengthalternative-regression-absent')
req(world.count('bool enabled;')<=1,'enabled-shadow-regression-absent',str(world.count('bool enabled;')))
req(len(re.findall(r'private\s+ServiceRegistry\?\s+_services\s*;',app))==1,'wpf-services-single')
req(len(re.findall(r'protected\s+override\s+async\s+void\s+OnStartup\s*\(',app))==1,'wpf-onstartup-single')
req(len(re.findall(r'protected\s+override\s+void\s+OnExit\s*\(',app))==1,'wpf-onexit-single')
req('void InitializeComponent' not in main and 'IComponentConnector.Connect' not in main,'wpf-generated-members-absent')

report={'schema':'majormud.pass32-classic-set-profile-settings-validation.v1','update':'0303','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_set_profile_pass32_static_validation_0303.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
