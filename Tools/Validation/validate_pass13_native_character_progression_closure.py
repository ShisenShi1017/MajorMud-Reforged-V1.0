import json, pathlib, sys
from collections import Counter
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
server=root/'Apps/Source/Reforged.Server'
checks=[]; errors=[]
def require(cond,code):
    (checks if cond else errors).append(code)
def text(path):
    return path.read_text(encoding='utf-8') if path.exists() else ''

formula_path=server/'RuntimeClassicCharacterProgressionFormula.cs'
progression_path=server/'RuntimeCharacterProgressionAuthority.cs'
foundation_path=server/'RuntimeCharacterFoundation.cs'
death_path=server/'RuntimeCharacterDeath.cs'
completion_path=server/'RuntimeCombatCompletion.cs'
world_path=server/'RuntimeWorldSystemsBundle.cs'
live_path=server/'RuntimeLiveGameplay.cs'
liveworld_path=server/'RuntimeLiveWorld.cs'
living_path=server/'RuntimeLivingWorld.cs'
plus_path=server/'RuntimeMajorMudPlus.cs'
character_formula_audit_path=server/'RuntimeClassicCharacterFormulaCalculator.cs'
parity_audit_path=server/'RuntimeMajorMudParityAudit.cs'
ledger_path=root/'Librarium/Parity/majormud_parity_assertions_0283.json'
evidence_path=root/'Librarium/Parity/majormud_character_progression_evidence_0283.json'
ability_path=root/'Librarium/Objects/abilities/ability_records.json'
rooms_path=root/'Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/rooms_decoded.json'
for p,code in [
    (formula_path,'progression-formula-source'),(progression_path,'progression-authority-source'),
    (foundation_path,'foundation-source'),(death_path,'death-source'),(completion_path,'combat-completion-source'),
    (world_path,'world-systems-source'),(live_path,'live-gameplay-source'),(living_path,'living-world-source'),
    (plus_path,'plus-source'),(character_formula_audit_path,'character-formula-audit-source'),
    (parity_audit_path,'parity-audit-source'),(ledger_path,'ledger-0283'),(evidence_path,'evidence-0283'),
    (ability_path,'ability-records'),(rooms_path,'decoded-rooms')]: require(p.exists(),code)

formula=text(formula_path); progression=text(progression_path); foundation=text(foundation_path)
death=text(death_path); completion=text(completion_path); world=text(world_path); live=text(live_path)
liveworld=text(liveworld_path); living=text(living_path); plus=text(plus_path); audit=text(character_formula_audit_path); parity=text(parity_audit_path)

# Exact recovered stock constants/branches.
for token,code in [
    ('ExperienceThresholdFormulaId = "character.stock_experience_threshold"','xp-formula-id'),
    ('CharacterPointAwardFormulaId = "character.stock_cp_award"','cp-formula-id'),
    ('PicklocksFormulaId = "character.stock_picklocks"','picklocks-formula-id'),
    ('PicklocksGrantAbilityId = 37','picklocks-grant-37'),('MaximumLives = 9','life-cap-nine'),
    ('MaxUInt32 = 4_294_967_295L','xp-uint32-boundary'),('Billion = 1_000_000_000L','xp-billion-tabulator'),
    ('characterClass.ExperienceStored + 100','class-exp-plus-100'),('race.ExperienceAddPercent','race-exp-add'),
    ('3 => (40, 20)','xp-mod-3'),('4 or 5 => (44, 24)','xp-mod-4-5'),('6 or 7 => (48, 28)','xp-mod-6-7'),
    ('8 or 9 => (52, 32)','xp-mod-8-9'),('10 or 11 => (56, 36)','xp-mod-10-11'),('12 or 13 => (60, 40)','xp-mod-12-13'),
    ('14 or 15 => (65, 45)','xp-mod-14-15'),('16 or 17 => (70, 50)','xp-mod-16-17'),('18 => (75, 55)','xp-mod-18'),
    ('<= 26 => (50, 40)','xp-mod-19-26'),('_ => (23, 20)','xp-mod-27'),
    ('if (level <= 55) return (115, 100)','xp-mod-28-55'),('if (level <= 58) return (109, 100)','xp-mod-56-58'),('return (108, 100)','xp-mod-59-plus'),
    ('(i / 10) * 5 + 10','cp-promotion-formula'),
    ('level <= 15','picklocks-level-15-branch'),('(((level - 15) / 2) + 15) * 2','picklocks-post-15-formula'),
    ('((levelComponent * 5) + checked(agility + intellect)) * 2','picklocks-stat-formula'),
    ('RuntimeCharacterAbilityValues.Read(acquiredValues, 180)','picklocks-acquired-bonus-180'),
    ('ResolveProjectedEquipmentContribution(','picklocks-no-double-count'),
]: require(token in formula,code)

# Runtime wiring.
for token,code in [
    ('Math.Min(RuntimeClassicCharacterProgressionFormula.MaximumLives, checked(currentLives + 1))','levelup-life-plus-one-cap'),
    ('RuntimeClassicCharacterProgressionFormula.RequiredExperience(targetLevel, race, characterClass)','training-xp-formula-row'),
    ('RuntimeClassicCharacterProgressionFormula.CharacterPointAwardForPromotion(targetLevel)','training-cp-formula-row'),
    ('if (!Directory.Exists(dataRoot)) return new RuntimeCharacterProgressionSchedule(rows);','formula-fallback-without-generated-progression-dir'),
]: require(token in progression,code)
require('["lives"] = RuntimeClassicCharacterProgressionFormula.MaximumLives' in foundation,'creation-starts-nine-lives')
require('Math.Max(0, currentLives - 1)' in death,'death-costs-one-life')
require('ReadLives(character) <= 0' in death,'zero-lives-block-respawn-resolver')
for token,code in [
    ('if (RuntimeClassicCharacterProgressionFormula.ReadLives(character) <= 0)','zero-lives-block-live-respawn'),
    ('evilPoints >= 40','outlaw-threshold-40'),('reputation.Equals("Outlaw"','outlaw-reputation'),
    ('reputation.Equals("Criminal"','criminal-reputation'),('reputation.Equals("Villain"','villain-reputation'),
    ('reputation.Equals("Villian"','villian-source-spelling-compat'),('reputation.Equals("Fiend"','fiend-reputation'),
    ('outlawOrWorse ? 142 : 2189','death-respawn-room-selection')]: require(token in completion,code)
require('schedule.Require(character, targetLevel, race, characterClass)' in world,'trainer-formula-aware-schedule')
require('RuntimeCharacterProgressionResolver.Promote(' in world,'trainer-promotes-through-authority')
require('ReadCondition(character.Conditions, "character_points", "cp", "creation_points")' in live,'status-earned-cp-first')
require('RuntimeClassicCharacterProgressionFormula.PicklocksValue(character)' in live,'status-live-picklocks')
require('schedule.Require(character, nextLevel, foundation.RequireRace(character.RaceId), foundation.RequireClass(character.ClassId))' in live,'exp-command-formula-aware')
require('RuntimeClassicCharacterProgressionFormula.PicklocksValue(character)' in living,'exit-gates-live-picklocks')
require('Math.Min(catalog.Settings.MaximumLives, RuntimeClassicCharacterProgressionFormula.MaximumLives)' in plus,'plus-life-cap-normalized-to-nine')
require(('majormud_parity_assertions_0283.json' in parity or 'majormud_parity_assertions_0284.json' in parity or 'majormud_parity_assertions_0285.json' in parity or 'majormud_parity_assertions_0286.json' in parity or 'majormud_parity_assertions_0287.json' in parity or 'majormud_parity_assertions_0288.json' in parity),'unified-ledger-current')

# C# formula audit integration.
for token,code in [
    ('stock-xp-level-one-zero','audit-xp-l1'),('stock-xp-level-fifty-chart-290-vector','audit-xp-l50'),
    ('stock-xp-uint-rollover-vector','audit-xp-rollover'),('stock-cp-level-eleven-award','audit-cp-l11'),
    ('stock-picklocks-post-fifteen-half-level-boundary','audit-picklock-boundary'),('stock-life-cap-nine','audit-life-cap')]:
    require(token in audit,code)

# Python mirror of stock formula vectors.
def modifier(level):
    if level <= 27:
        if level == 3: return 40,20
        if level in (4,5): return 44,24
        if level in (6,7): return 48,28
        if level in (8,9): return 52,32
        if level in (10,11): return 56,36
        if level in (12,13): return 60,40
        if level in (14,15): return 65,45
        if level in (16,17): return 70,50
        if level == 18: return 75,55
        if level <= 26: return 50,40
        return 23,20
    if level <= 55: return 115,100
    if level <= 58: return 109,100
    return 108,100

def xp(level,chart):
    MAX=4294967295; BILLION=1000000000
    running=0; billions=0; result=0
    for i in range(1,level+1):
        if i==1: running=0
        elif i==2: running=chart*10
        else:
            mul,div=modifier(i); potential=running*mul
            if potential>MAX:
                divides=0
                while potential>MAX:
                    running//=100; potential=running*mul; divides+=1
                alternate=(running*mul*100)//div if divides>1 else potential//div
                for _ in range(divides): alternate*=100
            else: alternate=potential//div
            j=1000000*mul*billions
            while j>MAX: j-=MAX+1
            while j>=BILLION: j-=BILLION; billions+=1
            k=j+alternate
            while k>=BILLION: k-=BILLION; billions+=1
            running=k
        result=running+billions*BILLION
    return result
for level,chart,expected in [(1,100,0),(2,100,1000),(3,100,2000),(10,100,81123),(50,290,783554100),(100,290,51766413104),(255,290,222704287320)]:
    require(xp(level,chart)==expected,f'xp-vector:{level}:{chart}:{expected}')
def cp_award(level): return 0 if level<=1 else ((level-1)//10)*5+10
for level,expected in [(2,10),(10,10),(11,15),(20,15),(21,20),(31,25)]: require(cp_award(level)==expected,f'cp-vector:{level}:{expected}')
def pick(level,agl,intel):
    lc=level*2 if level<=15 else (((level-15)//2)+15)*2
    return (((lc*5)+(agl+intel))*2)//7
for v in [(1,50,50,31),(15,70,70,82),(16,70,70,82),(17,70,70,85),(30,100,100,120)]: require(pick(*v[:3])==v[3],f'picklocks-vector:{v}')

# Ability semantics and decoded death rooms.
abilities=json.loads(ability_path.read_text(encoding='utf-8'))['records'] if ability_path.exists() else []
by_ability={}
for a in abilities:
    legacy=a.get('public_reference_0139',{}).get('fields',{}).get('legacy_number',a.get('legacy_id',-1))
    try: by_ability[int(legacy)]=a
    except Exception: pass
require(by_ability.get(37,{}).get('name')=='Picklocks','ability-37-picklocks-grant')
require('grant' in by_ability.get(37,{}).get('public_reference_0139',{}).get('fields',{}).get('description','').lower(),'ability-37-description-grant')
require(by_ability.get(180,{}).get('name')=='PickLocksValue','ability-180-picklocks-value')
rooms=json.loads(rooms_path.read_text(encoding='utf-8')) if rooms_path.exists() else {}
# room corpus shapes have changed over updates; walk arbitrary JSON objects.
found={}
def walk(node):
    if isinstance(node,dict):
        mapv=node.get('map') or node.get('map_id') or node.get('mapId') or node.get('map_number') or node.get('mapNumber')
        roomv=node.get('room') or node.get('room_number') or node.get('roomNumber') or node.get('number')
        name=node.get('name') or node.get('room_name') or node.get('roomName')
        try:
            key=(int(mapv),int(roomv))
            if key in {(1,142),(1,2189)} and name: found[key]=str(name)
        except Exception: pass
        for v in node.values(): walk(v)
    elif isinstance(node,list):
        for v in node: walk(v)
walk(rooms)
require('Earthen Tomb' in found.get((1,142),''),'decoded-earthen-tomb-1-142')
require('Halls of the Dead' in found.get((1,2189),''),'decoded-halls-dead-1-2189')

# Ledger: retire broad XP/CP unknown and promote exact branch ownership conservatively to STRONG.
ledger=json.loads(ledger_path.read_text(encoding='utf-8')) if ledger_path.exists() else {'assertions':[]}
require(ledger.get('update')=='0283','ledger-update-0283')
by={a['id']:a for a in ledger.get('assertions',[])}
require('character.absolute-xp-cp-schedule' not in by,'broad-xp-cp-unknown-retired')
for i,state in {
    'character.picklocks-stock-formula':'STRONG',
    'character.stock-xp-threshold-formula':'STRONG',
    'character.stock-cp-award-schedule':'STRONG',
    'character.life-gain-on-level':'STRONG',
    'character.life-loss-on-death':'PASS',
    'character.lives-starting-maximum':'PASS',
    'character.permadeath-zero-lives':'PASS',
    'character.death-alignment-respawn':'STRONG',
    'character.death-xp-loss-exact':'UNKNOWN',
}.items(): require(by.get(i,{}).get('state')==state,f'ledger:{i}:{state}')

# Project score, including remaining native dependency groups exactly as RuntimeMajorMudParityAudit does.
rem=json.loads((root/'Librarium/Reports/COMBINED_DECODE_REMAINING_AUDIT_AFTER_REMAINING_SYSTEMS.json').read_text(encoding='utf-8'))
assertions=list(ledger.get('assertions',[])); known={a['id'] for a in assertions}
for item in rem.get('deduplicatedRemainingSystemsNativeDependencies',[]):
    aid='native.'+item.get('id','')
    if aid in known: continue
    assertions.append({'id':aid,'state':'UNKNOWN'})
counts=Counter(a['state'] for a in assertions)
require(counts['PASS']==69,'score-pass-69')
require(counts['STRONG']==56,'score-strong-56')
require(counts['UNKNOWN']==48,'score-unknown-48')
require(len(assertions)==173,'score-total-173')
require(round((counts['PASS']+counts['STRONG'])*100/len(assertions),1)==72.3,'score-evidence-72.3')

# Historical pass validators must point at the current unified ledger without changing their own historical ledger checks.
for rel in ['Tools/Validation/validate_pass10_npc_parity.py','Tools/Validation/validate_pass11_rooms_doors_traps_parity.py','Tools/Validation/validate_pass12_economy_transactions_services_parity.py']:
    t=text(root/rel); require(('majormud_parity_assertions_0283.json' in t or 'majormud_parity_assertions_0284.json' in t or 'majormud_parity_assertions_0285.json' in t or 'majormud_parity_assertions_0286.json' in t or 'majormud_parity_assertions_0287.json' in t or 'majormud_parity_assertions_0288.json' in t),f'historical-current-pointer:{pathlib.Path(rel).name}')

result={'passed':not errors,'check_count':len(checks)+len(errors),'checks':checks,'errors':errors,
        'score':{'PASS':counts['PASS'],'STRONG':counts['STRONG'],'UNKNOWN':counts['UNKNOWN'],'TOTAL':len(assertions),'evidence_backed_percent':round((counts['PASS']+counts['STRONG'])*100/len(assertions),1)}}
out=root/'Reports/RuntimeVerification/native_character_progression_pass13_static_validation_0283.json'
out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
sys.exit(0 if not errors else 1)
