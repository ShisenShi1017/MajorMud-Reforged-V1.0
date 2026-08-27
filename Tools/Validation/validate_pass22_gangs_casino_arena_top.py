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

ledger=js('Librarium/Parity/majormud_parity_assertions_0292.json')
counts=Counter(a['state'] for a in ledger['assertions'])
check(len(ledger['assertions'])==179,'ledger-total')
check(counts==Counter({'PASS':73,'STRONG':73,'UNKNOWN':33}),'ledger-score',str(counts))
for aid in ['native.games-arenas-optional-activities','native.rankings-statistics','native.organizations.persistence-services']:
    a=next(x for x in ledger['assertions'] if x['id']==aid)
    check(a['state']=='STRONG','promoted-strong:'+aid)

casino=text('Apps/Source/Reforged.Server/RuntimeClassicCasino.cs')
for token,name in [
    ('room.map1.2','casino-room-1-2'),('room.map1.10','casino-room-1-10'),('room.map1.11','casino-room-1-11'),
    ('"draw cards"','dead-jester-draw-cards'),('10_000','casino-100gold-copper-value'),
    ('Random.Shared.Next(1, 101)','global-rng-explicitly-temporary'),('DecodedAddDelayValue','decoded-delay-preserved'),
    ('Casino result textblock #','unrecovered-prose-not-fabricated')]: check(token in casino,name)
check('Outcomes((8,1,9143)' in casino,'dead-jester-table-start')
check('(100,100_000,9156)' in casino,'dead-jester-table-end')
check('(100,150_000,1019)' in casino,'room-1-2-dice-table-end')
check('(100,5_000,9157)' in casino,'room-1-11-slot-table-end')

living=text('Apps/Source/Reforged.Server/RuntimeLivingWorld.cs')
check('TryExecuteClassicCasinoCommand(session, command, out result)' in living,'casino-hook-before-world-object-fallback')

native=text('Librarium/Evidence/native_message_catalog_correlation_0022.json')
for phrase,name in [
    ('Create Gang <gang name>','native-create-gang'),('Requires','native-gang-create-requirement'),
    ('Max name length of 19 characters','native-gang-name-19'),('Practice Arena colliseum','native-practice-arena-title'),
    ('no fear of evil points or loss of life','native-practice-arena-no-loss'),('Every sundown','native-ganghouse-sundown'),
    ('Silvermere bank account','native-ganghouse-bank'),('is used to promote a member','native-promote-help'),
    ('when the gangleader is not online','native-lieutenant-offline-rule'),
    ('Will display the top ten adventurers in the game.','native-topten-adventurers')]:
    check(phrase.lower() in native.lower(),name)

scripts=text('Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142/textblocks_decoded.json')
for phrase,name in [
    ('draw cards:price 10000 1560:random 9134','dead-jester-command-script'),
    ('play dragon\'s dice:price 10000 1560:random 998','room-1-2-dice-script'),
    ('play slots:price 1000 1560:random 1020','room-1-2-slot-script'),
    ('play dice:price 1000 1560:random 9136:adddelay 5','room-1-11-dice-script'),
    ('play crowns and skulls:price 1000 1560:random 9137:adddelay 5','room-1-11-crowns-script')]:
    check(phrase in scripts,name)

payload=text('Librarium/Objects/native_textblock_payloads/native_textblock_payload_records.json')
check('Wager: 100 gold coins' in payload or '100 gold' in payload,'dead-jester-100-gold-sign')
check('DRAW CARDS' in payload,'dead-jester-native-command-sign')

gang=text('Apps/Source/Reforged.Server/RuntimeClassicGangState.cs')
check('leader.Experience < 100000' in gang,'gang-create-xp')
check('name.Length is < 1 or > 19' in gang,'gang-name-limit')
check('member.Role.Equals("Lieutenant"' in gang and '&& !leaderOnline' in gang,'lieutenant-only-when-leader-offline')
gameplay=text('Apps/Source/Reforged.Server/RuntimeClassicGangGameplay.cs')
check('leaderOnline = _sessions.FindByActor(gang.LeaderCharacterId) is not null' in gameplay,'leader-online-observed')
check('.OrderByDescending(character => character.Experience)' in gameplay,'character-top-experience-order')
check('RuntimeCharacterFlagIds.ToptenDisabled' in gameplay,'character-top-notopten-filter')
check('ClassName.Equals(classFilter' in gameplay,'class-filter-top-live')

upkeep=text('Apps/Source/Reforged.Server/RuntimeClassicGangHouseUpkeep.cs')
check('DebitBankGold' in upkeep,'ganghouse-silvermere-bank-debit')
check('RepossessHouse' in upkeep,'ganghouse-repossession')

arena=text('Apps/Source/Reforged.Server/RuntimeLivePvp.cs')
check('CompletePracticeArenaPlayerDefeat' in arena,'practice-arena-defeat-live')
check('RequirePracticeArenaEntryHitPoints' in arena,'practice-arena-entry-hp-restored')

registry=js('Librarium/RemainingSystems/remaining_systems_registry.json')
for sid in ['games-arenas-optional-activities','rankings-statistics','guild-faction-organization']:
    s=next(x for x in registry['systems'] if x['id']==sid)
    check('strong' in (s.get('classification','')+s.get('evidenceClassification','')).lower(),'registry-strong:'+sid)

score=js('Reports/RuntimeVerification/majormud_parity_scorecard_0292.json')
check((score['PassCount'],score['StrongCount'],score['UnknownCount'],score['ScoredRequirementCount'])==(73,73,33,179),'scorecard-counts')
check(score['StrictPercent']==40.8,'scorecard-strict')
check(score['EvidenceBackedPercent']==81.6,'scorecard-backed')

audit=text('Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs')
check('majormud_parity_assertions_0292.json' in audit,'runtime-audit-ledger-pointer')
check('majormud_ownership_scope_0291.json' in audit,'runtime-audit-scope-preserved')

server=text('Apps/Source/Reforged.Server/ServerRuntimeHost.cs')
check('public sealed partial class ServerRuntimeHost' in server,'borin-partial-host-preserved')
check('RuntimeClassicTimingAuthority.PlayerDataSaveInterval' in server,'classic-60-second-save-preserved')
check('TimeSpan.FromMinutes(5)' not in server,'trainer-autosave-regression-not-reintroduced')

for rel in ['Librarium/Parity/majormud_parity_assertions_0292.json','Librarium/Parity/majormud_gangs_casino_arena_top_evidence_0292.json','Librarium/RemainingSystems/remaining_systems_registry.json','Reports/RuntimeVerification/majormud_parity_scorecard_0292.json']:
    try: js(rel); check(True,'json:'+rel)
    except Exception as e: check(False,'json:'+rel,str(e))

report={'Passed':not failures,'CheckCount':len(checks)+len(failures),'PassedCount':len(checks),'FailureCount':len(failures),'Checks':checks,'Failures':failures}
out=root/'Reports/RuntimeVerification/gangs_casino_arena_top_pass22_static_validation_0292.json'
out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
sys.exit(0 if not failures else 1)
