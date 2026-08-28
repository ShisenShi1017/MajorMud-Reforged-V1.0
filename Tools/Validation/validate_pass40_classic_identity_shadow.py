#!/usr/bin/env python3
import hashlib,json,re
from pathlib import Path
from collections import Counter
root=Path(__file__).resolve().parents[2]
failures=[]; checks=[]
def req(cond,name,detail=''):
    checks.append(name)
    if not cond: failures.append({'check':name,'detail':detail})
def text(rel): return (root/rel).read_text(errors='replace')
def js(rel): return json.loads(text(rel))
def sha(rel): return hashlib.sha256((root/rel).read_bytes()).hexdigest()

evidence=js('Librarium/Parity/majormud_character_identity_shadow_evidence_0311.json')
ledger=js('Librarium/Parity/majormud_parity_assertions_0311.json')
prior=js('Librarium/Parity/majormud_parity_assertions_0310.json')
score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0311.json')
remain=js('Librarium/RemainingSystems/remaining_systems_registry.json')
abilities=js('Librarium/Objects/abilities/ability_records.json')
items=js('Librarium/Objects/items/item_records_0122.json')
rels=js('Librarium/Validation0003/item_ability_relationships_verified_0003.json')
appearance=text('Apps/Source/Reforged.Server/RuntimeClassicCharacterAppearance.cs')
live=text('Apps/Source/Reforged.Server/RuntimeLiveWorld.cs')
registry=text('Apps/Source/Reforged.Server/RuntimeClassicIdentityShadowEvidenceRegistry.cs')
smoke=text('Apps/Source/Reforged.Server/RuntimePass40ClassicIdentityShadowVerificationSmokeReport.cs')
program=text('Apps/Source/Reforged.Server/Program.cs')
audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
doc=text('Docs/CODE_UPDATE_0311_CLASSIC_CHARACTER_IDENTITY_SHADOW.md')
evdoc=text('Librarium/Evidence/Character/CLASSIC_CHARACTER_IDENTITY_SHADOW_PASS_0311.md')

req(evidence['schema']=='majormud.character-identity-shadow-evidence.v1','evidence-schema')
req(evidence['pass']==40 and evidence['update']=='0311','evidence-pass-update')
req(evidence['noGameplayGuessing'] is True,'evidence-no-guessing')
req(evidence['runtimeProjection']['roomOccupantList'].startswith('Unchanged'),'room-list-explicitly-unchanged')
req(evidence['parityPromotion']=={'assertion':'native.character-identity-policy','from':'UNKNOWN','to':'STRONG','reason':evidence['parityPromotion']['reason']},'promotion-shape')
for claim in ['3-15 character name limit from the user-provided page','letters-only name grammar','automatic capitalization rule','complete reserved-name list','exact title precedence','exact invisibility/True Sight identity rendering','room occupant list replacement with A shadowy figure','exact identity validation/failure messages']:
    req(claim in evidence['notPromoted'],'not-promoted-'+re.sub(r'\W+','-',claim)[:34])

# retained ability 9
arows=abilities['records'] if isinstance(abilities,dict) and 'records' in abilities else abilities
shadow=[x for x in arows if str(x.get('legacy_id'))=='9']
req(len(shadow)==1,'ability9-single')
if shadow:
    f=shadow[0].get('public_reference_0139',{}).get('fields',{})
    req(f.get('name')=='Shadow','ability9-name')
    req(f.get('description')=='Displays "A shadowy figure!" when looked at by other players.','ability9-description-exact',str(f.get('description')))
    req(f.get('impl_status')=='implemented','ability9-reference-implemented')

# item 1331 and relationship
rows=items['records'] if isinstance(items,dict) and 'records' in items else items
robes=[x for x in rows if x.get('id')=='item.1331']
req(len(robes)==1,'item1331-single')
if robes:
    r=robes[0]
    req(r.get('name')=='black embroidered robes','item1331-name')
    abs_=r.get('public_reference_0139',{}).get('fields',{}).get('abilities_named',[])
    sh=[a for a in abs_ if a.get('ability_id')==9]
    req(len(sh)==1,'item1331-shadow-single')
    if sh: req(str(sh[0].get('value'))=='100','item1331-shadow-100')
rel=[x for x in rels if x.get('itemId')==1331 and x.get('abilityId')==9]
req(len(rel)==1,'verified-item1331-shadow-relation-single')
if rel:
    req(rel[0].get('abilityName')=='Shadow','verified-shadow-name')
    req(str(rel[0].get('value'))=='100','verified-shadow-value')
    req(rel[0].get('evidenceState')=='Verified','verified-shadow-state')
    req(rel[0].get('confidence')==100,'verified-shadow-confidence')

# runtime helper and consumer
for tok in ['public const int ShadowAbilityId = 9;','public const string ShadowLookPayload = "A shadowy figure!";','TryResolveEquippedShadowLookOverride','item.EquippedSlot is null','EquipmentAbilityModifiers.GetValueOrDefault(ShadowAbilityId) <= 0','if (isSelf || character.ItemState is null) return false;']:
    req(tok in appearance,'appearance-'+re.sub(r'\W+','-',tok)[:38])
for tok in ['TryResolveEquippedShadowLookOverride(record, catalog, isSelf, out var shadowPayload)','var visibleIdentity = shadowLook ? "A shadowy figure" : character.DisplayName;','if (shadowLook)','lines.Add(shadowPayload);','shadowLook ? "a shadowy figure" : character.DisplayName']:
    req(tok in live,'live-'+re.sub(r'\W+','-',tok)[:38])
# Room list remains real-name based and is not routed through the LOOK mask.
room_block=live[live.index('var nativePlayers = roomSessions.Select'):live.index('var occupants = RuntimeMajorMudCompatibility.FormatRoomOccupants')]
req('other.Actor.DisplayName' in room_block,'room-occupants-still-displayname')
req('TryResolveEquippedShadowLookOverride' not in room_block,'room-occupants-not-shadow-masked')
req('shadowLook ? "A shadowy figure"' not in room_block,'room-occupants-no-shadow-alias')

# registry and smoke
for tok in ['ShadowAbilityId = 9','BlackEmbroideredRobesLegacyId = 1331','BlackEmbroideredRobesShadowValue = 100','ShadowLookPayload = "A shadowy figure!"','expected-eight-identity-shadow-evidence-rows','expected-three-strong-shadow-rows','expected-three-open-identity-rows']:
    req(tok in registry,'registry-'+re.sub(r'\W+','-',tok)[:38])
for tok in ['identity-shadow-evidence-registry-valid','shadow-ability-id-9','black-embroidered-robes-item-1331','black-embroidered-robes-shadow-100','shadow-look-payload-exact']:
    req(tok in smoke,'smoke-'+tok)
req('--smoke-pass40-classic-identity-shadow-verification' in program,'pass40-cli')
req('RuntimePass40ClassicIdentityShadowVerificationSmokeReport.Run()' in program,'pass40-run')
req(program.count('--smoke-pass40-classic-identity-shadow-verification')==1,'pass40-cli-single')
req('majormud_parity_assertions_0316.json' in audit,'audit-current-0311')
req('majormud_parity_assertions_0310.json' not in audit,'audit-prior-retired')

# exactly one assertion changes UNKNOWN -> STRONG
old={a['id']:a for a in prior['assertions']}; cur={a['id']:a for a in ledger['assertions']}
req(prior['update']=='0310','prior-update-0310')
req(ledger['update']=='0311','ledger-update-0311')
req(len(old)==200 and len(cur)==200,'ledger-total-200')
req(old['native.character-identity-policy']['state']=='UNKNOWN','prior-identity-unknown')
req(cur['native.character-identity-policy']['state']=='STRONG','current-identity-strong')
changed=[(aid,old[aid]['state'],row['state']) for aid,row in cur.items() if old[aid]['state']!=row['state']]
req(changed==[('native.character-identity-policy','UNKNOWN','STRONG')],'only-identity-promotion',str(changed))
counts=Counter(a['state'] for a in ledger['assertions'])
req((counts['PASS'],counts['STRONG'],counts['UNKNOWN'])==(97,81,22),'ledger-counts-97-81-22',str(counts))
for aid in ['character.death-xp-loss-exact','commands.native-player-command-table','native.law-bounty-guards']:
    req(cur[aid]['state']=='UNKNOWN','neighbor-remains-unknown-'+aid)

req((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(97,81,22,200),'score-counts')
req(abs(score['StrictPercent']-48.5)<0.01,'score-strict')
req(abs(score['EvidenceBackedPercent']-89.0)<0.01,'score-evidence')
cc=next(c for c in score['Categories'] if c['Category']=='Character & Status')
req((cc['Pass'],cc['Strong'],cc['Unknown'],cc['Total'])==(24,19,3,46),'character-category-counts')
req(abs(cc['EvidenceBackedPercent']-93.5)<0.01,'character-category-evidence')
req(remain.get('updatedByPass')in {'Pass 40 / Update 0311','Pass 41 / Update 0312','Pass 42 / Update 0313','Pass 43 / Update 0314','Pass 44 / Update 0315','Pass 45 / Update 0316'},'remaining-update-0311')
ids={s['id']:s for s in remain['systems']}
ident=ids['character-identity-policy']
req(ident.get('status')=='implemented-strong-shadow-look-boundary','remaining-identity-status')
req(ident.get('shadowLook0311',{}).get('abilityId')==9,'remaining-shadow-id')
req(ident.get('shadowLook0311',{}).get('roomOccupantNameMasked')=='UNKNOWN; unchanged in runtime','remaining-room-gate')

for token in ['UNKNOWN -> STRONG','A shadowy figure!','item.1331','Room occupant','3-15','97 PASS / 81 STRONG / 22 UNKNOWN = 200']:
    req(token.lower() in doc.lower() or token.lower() in evdoc.lower(),'doc-'+re.sub(r'\W+','-',token)[:30])

protected={
 'Apps/Source/Reforged.App/App.xaml.cs':'7859f07246d9891f83b3c1c99fadd332ef514c5cc4ee59dc90276a1c11e5f397',
 'Apps/Source/Reforged.App/MainWindow.xaml.cs':'4bfb84db25a6798780d76300a7f4f58dd63d3ebeaec035b4a1eaf266aefc325b',
 'Apps/Source/Reforged.App/Brains/BorinHeadlessTrainingService.cs':'f39861f8f2cf00d8641f694b7f2a022353a00b48782aba0b3bd6575cb5aaede8',
 'Apps/Source/Reforged.App/ViewModels/BorinForgeViewModel.cs':'51c6558509d2b7b08605803cb8137ff094d1d79f797f76e707de1192094f3cc9',
 'Apps/Source/Reforged.App/Services/CustomWorldSimpleFormCatalog.cs':'7368ef1919c30ae89b35d04ae6d8c364dfb30c4d241fe0c9df8541494289a52a',
 'Apps/Source/Reforged.App/ViewModels/EditorViewModel.cs':'6233ccb692a7088ddfb5aab20f2144ba8fc9657ad672fafa87aba6a9ae9da791',
 'Apps/Source/Reforged.App/Views/EditorView.xaml':'ba66c5e2894ab4b34a87ac73bb3e89ac31c2153111d9ade00180ed345534f486'}
for rel,expected in protected.items():
    req((root/rel).exists(),'protected-exists-'+Path(rel).name)
    req(sha(rel)==expected,'protected-hash-'+Path(rel).name)
ini=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name.lower()=='wccmmud.ini']
req(not ini,'no-wccmmud-ini',str(ini))

report={'schema':'majormud.pass40-classic-identity-shadow-validation.v1','update':'0311','status':'PASS' if not failures else 'FAIL','checks':len(checks),'failures':failures}
out=root/'Reports/RuntimeVerification/classic_identity_shadow_pass40_static_validation_0311.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 1)
