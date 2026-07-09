#!/usr/bin/env python3
from __future__ import annotations
import csv, json, os, re, hashlib, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(sys.argv[1]) if len(sys.argv)>1 else Path('ImportedDatabases')
OUT = Path(sys.argv[2]) if len(sys.argv)>2 else Path('Librarium/MajorMUDKnowledgeBase')
OUT.mkdir(parents=True, exist_ok=True)

SOURCE_META = {
    'NightmareRedux': {'priority':2, 'role':'primary_current_reverse_engineered_implementation'},
    'MBBSEmu': {'priority':3, 'role':'independent_emulator_runtime_reference'},
    'MMUDExplorer': {'priority':4, 'role':'data_structure_and_field_interpretation'},
    'MonsterSim': {'priority':5, 'role':'historical_behavior_simulator'},
}
TEXT_EXTS = {'.txt','.md','.bas','.cls','.ctl','.frm','.vbp','.vbw','.cs','.vb','.php','.json','.xml','.yml','.yaml','.ini','.cfg','.csv','.sql','.bat','.ps1','.sh','.html','.htm','.xaml','.sln','.csproj','.vbproj','.resx','.manifest','.config','.gitignore','.dockerignore'}
BINARY_EXTS = {'.mdb','.accdb','.xls','.xlsx','.png','.jpg','.jpeg','.gif','.ico','.dll','.exe','.pdb','.zip','.gz','.7z','.pdf'}

BUCKETS = [
('combat',['combat','attack','attacks','damage','dmg','weapon','swing','dr','armor','ac','dodge','block','parry','crit','critical','backstab','tohit','hit chance','miss']),
('magic',['spell','magic','mana','cast','casting','mr','resist','resistance','buff','debuff','curse','duration']),
('npc_ai',['monster','npc','mob','ai','aggro','hate','target','flee','wander','behavior','enemy']),
('items_equipment',['item','equip','equipment','armor','weapon','wear','worn','modifier','proc','charge','effect','loot']),
('character_progression',['level','experience','exp','xp','class','race','train','stat','strength','agility','intellect','hp','mp','gain']),
('skills_abilities',['skill','ability','stealth','pick','bash','hide','sneak','tracking','ability']),
('economy_shops',['shop','store','gold','silver','copper','price','buy','sell','repair','bank','economy']),
('world_spawns',['room','spawn','respawn','map','exit','event','timer','zone','world','location']),
('quests_scripts',['quest','script','trigger','flag','special','action','room action','scripted']),
('guilds_factions',['guild','faction','alignment','reputation']),
('commands_client',['command','client','input','output','prompt','terminal','telnet','bbs','ansi']),
('data_schema',['schema','record','field','offset','byte','parse','parser','deserialize','serialize','database','mdb','table']),
('tests_validation',['test','assert','expected','compare','sim','simulation','benchmark','validation']),
('docs_notes',['readme','doc','documentation','note','comment','todo','manual']),
('tooling_ui',['form','editor','button','click','menu','dialog','ui','visual','control','designer','property']),
('build_meta',['github','workflow','project','solution','license','docker','build','package','funding']),
('networking_runtime',['server','session','connection','packet','socket','module','runtime','engine']),
('logging_debug',['log','debug','trace','error','exception']),
('asset_resource',['image','icon','sound','resource','asset']),
('non_gameplay',['license','funding','gitignore','gitattributes','dockerignore'])]

MECH_PATTERNS = [
('COMBAT_DR', re.compile(r'\bDR\b|damage reduction|damagereduction|DamageReduction', re.I)),
('COMBAT_DODGE', re.compile(r'dodge|evade|evasion', re.I)),
('COMBAT_HIT_AC', re.compile(r'\bAC\b|armor class|to.?hit|hit chance|miss', re.I)),
('COMBAT_BACKSTAB_SPECIAL', re.compile(r'backstab|special attack|specialattack|sneak attack', re.I)),
('COMBAT_CRITICAL', re.compile(r'critical|\bcrit\b', re.I)),
('MAGIC_RESIST', re.compile(r'magic resist|\bMR\b|resist', re.I)),
('MAGIC_SPELL_ENERGY', re.compile(r'spell.*energy|energy.*spell|half.*energy|cost.*500|cost.*1000', re.I)),
('MAGIC_MANA_CAST', re.compile(r'mana|cast|spell cost|spellcost', re.I)),
('NPC_ATTACK_LOOP', re.compile(r'attack.*energy|energy.*attack|swing|attacks per|double spell|second spell', re.I)),
('ITEM_EFFECTS', re.compile(r'item.*effect|effect.*item|modifier|bonus|proc|charge', re.I)),
('DATA_FIELDS', re.compile(r'offset|field|record|byte|bit|flag|parse|table', re.I)),
('SPAWN_TIMERS', re.compile(r'spawn|respawn|timer', re.I)),
('ECONOMY', re.compile(r'price|shop|buy|sell|repair|bank|gold', re.I)),
('XP_LEVEL', re.compile(r'experience|\bxp\b|level|train', re.I)),
]

def source_name(path: Path) -> str:
    parts=path.relative_to(ROOT).parts
    return parts[0] if parts else 'unknown'

def is_text_file(p: Path) -> bool:
    ext=p.suffix.lower()
    if ext in TEXT_EXTS: return True
    if ext in BINARY_EXTS: return False
    try:
        data=p.read_bytes()[:4096]
        if b'\x00' in data: return False
        data.decode('utf-8')
        return True
    except Exception:
        return False

def read_text(p: Path):
    data=p.read_bytes()
    for enc in ('utf-8','latin-1','cp1252'):
        try:
            return data.decode(enc), enc
        except UnicodeDecodeError:
            continue
    return data.decode('latin-1', errors='ignore'), 'latin-1-ignore'

def classify(p: Path, text: str|None):
    rel=str(p.relative_to(ROOT)).replace('\\','/')
    hay=(rel+'\n'+(text[:20000] if text else '')).lower()
    scores=[]
    for bucket, keys in BUCKETS:
        s=sum(hay.count(k.lower()) for k in keys)
        # filename/path bonus
        if any(k.lower() in rel.lower() for k in keys): s+=3
        if s: scores.append((s,bucket))
    scores.sort(reverse=True)
    primary=scores[0][1] if scores else ('asset_resource' if p.suffix.lower() in BINARY_EXTS else 'non_gameplay')
    secondary=[b for s,b in scores[1:5] if s>0]
    relevance='non_gameplay_or_support'
    if primary in {'combat','magic','npc_ai','items_equipment','character_progression','skills_abilities','economy_shops','world_spawns','quests_scripts','guilds_factions'}:
        relevance='gameplay_mechanic_candidate'
    elif primary in {'data_schema','tests_validation','docs_notes'}:
        relevance='supporting_evidence'
    elif primary in {'tooling_ui','commands_client','networking_runtime'}:
        relevance='implementation_support'
    return primary, secondary, relevance, scores[:8]

inventory=[]; hits=[]; dup_hashes={}
for p in sorted(ROOT.rglob('*')):
    if not p.is_file(): continue
    rel=str(p.relative_to(ROOT)).replace('\\','/')
    src=source_name(p)
    data=p.read_bytes()
    sha=hashlib.sha256(data).hexdigest()
    dup_hashes.setdefault(sha,[]).append(rel)
    text=None; enc=None; lines=0; text_state='binary_or_unread'
    if is_text_file(p):
        text,enc=read_text(p)
        lines=text.count('\n')+1
        text_state='text'
    primary,secondary,relevance,scores=classify(p,text)
    inv={
        'source':src,'source_priority':SOURCE_META.get(src,{}).get('priority'),
        'path':rel,'extension':p.suffix.lower(),'size_bytes':len(data),'sha256':sha,
        'text_state':text_state,'encoding':enc,'line_count':lines,
        'primary_bucket':primary,'secondary_buckets':secondary,'relevance':relevance,
        'classification_scores':scores
    }
    inventory.append(inv)
    if text:
        for i,line in enumerate(text.splitlines(),1):
            # avoid massive UI/designer noise, but still classify by patterns
            for mech,pat in MECH_PATTERNS:
                if pat.search(line):
                    stripped=line.strip()
                    if stripped:
                        hits.append({'source':src,'source_priority':SOURCE_META.get(src,{}).get('priority'), 'path':rel,'line':i,'mechanic_id':mech,'bucket':primary,'text':stripped[:400]})
                    break

# duplicates across sources only
cross_dups=[]
for sha,paths in dup_hashes.items():
    srcs={p.split('/')[0] for p in paths}
    if len(paths)>1 and len(srcs)>1:
        cross_dups.append({'sha256':sha,'sources':sorted(srcs),'paths':paths})

# conflicts / leading interpretations from known evidence policy
conflicts=[
 {'id':'CONFLICT_COMBAT_DR_001','mechanic':'Physical Damage Reduction','status':'open_leading_nightmare_redux','current_policy':'Prefer Nightmare Redux 2025 over historical Monster Sim when unsupported by original observations; seek MBBSEmu/runtime corroboration before classic-verified.', 'sources':['NightmareRedux','MonsterSim']},
 {'id':'CONFLICT_COMBAT_DODGE_ORDER_001','mechanic':'Dodge/Evasion order','status':'open_needs_order_confirmation','current_policy':'Nightmare Redux is leading interpretation; confirm sequence against emulator/log behavior before runtime lock.', 'sources':['NightmareRedux','MonsterSim','MMUDExplorer']},
 {'id':'CONFLICT_BACKSTAB_SPECIAL_DEFENSE_001','mechanic':'Backstab/special attack defense order','status':'open_needs_specific_scenario_tests','current_policy':'Treat as unresolved until cross-source scenario tests exist.', 'sources':['NightmareRedux','MMUDExplorer']},
]

summary={
 'generated_utc':datetime.now(timezone.utc).isoformat(),
 'phase':'Phase 1 Exhaustive Canonical Evidence Mining',
 'definition_of_exhausted':'Every file in the four canonical source packages was inventoried, assigned a classification, and either linked to mechanic evidence or marked as supporting/non-gameplay.',
 'sources':SOURCE_META,
 'file_count':len(inventory),
 'text_files':sum(1 for x in inventory if x['text_state']=='text'),
 'binary_or_unread_files':sum(1 for x in inventory if x['text_state']!='text'),
 'evidence_hit_count':len(hits),
 'cross_source_duplicate_groups':len(cross_dups),
 'open_conflict_count':len(conflicts),
 'runtime_modified':False,
 'by_source':{}, 'by_bucket':{}, 'by_relevance':{}
}
for x in inventory:
    summary['by_source'].setdefault(x['source'],0); summary['by_source'][x['source']]+=1
    summary['by_bucket'].setdefault(x['primary_bucket'],0); summary['by_bucket'][x['primary_bucket']]+=1
    summary['by_relevance'].setdefault(x['relevance'],0); summary['by_relevance'][x['relevance']]+=1

(OUT/'phase1_exhaustive_source_inventory.json').write_text(json.dumps(inventory,indent=2),encoding='utf-8')
(OUT/'phase1_exhaustive_evidence_hits.json').write_text(json.dumps(hits,indent=2),encoding='utf-8')
(OUT/'phase1_exhaustive_duplicate_groups.json').write_text(json.dumps(cross_dups,indent=2),encoding='utf-8')
(OUT/'phase1_exhaustive_conflict_log.json').write_text(json.dumps(conflicts,indent=2),encoding='utf-8')
(OUT/'phase1_exhaustive_summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')

with (OUT/'phase1_exhaustive_source_inventory.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=['source','source_priority','path','extension','size_bytes','sha256','text_state','line_count','primary_bucket','secondary_buckets','relevance'])
    w.writeheader()
    for x in inventory:
        row={k:x[k] for k in w.fieldnames}; row['secondary_buckets']=';'.join(row['secondary_buckets']); w.writerow(row)
with (OUT/'phase1_exhaustive_evidence_hits.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=['source','source_priority','path','line','mechanic_id','bucket','text'])
    w.writeheader(); w.writerows(hits)
print(json.dumps(summary,indent=2))
