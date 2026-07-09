#!/usr/bin/env python3
"""Build local Librarium distribution artifacts.

This tool intentionally generates local artifacts from canonical repository files.
Generated outputs should not be committed unless explicitly approved.

Usage from repo root:
  python Tools/LibrariumDistribution/build_librarium_distribution.py

Outputs:
  Generated/Librarium/librarium.sqlite
  Generated/Librarium/core_graph.json
  Generated/Librarium/query_index.json
  Generated/Librarium/runtime_index.json
  Generated/Librarium/evidence_index.json
"""
from __future__ import annotations
import json, sqlite3, hashlib, re
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Generated" / "Librarium"
OUT.mkdir(parents=True, exist_ok=True)
DB = OUT / "librarium.sqlite"
TEXT_EXT = {'.md','.json','.yaml','.yml','.txt','.csv'}
CANONICAL_DIRS = ['Librarium','Evidence','Validation','Reports','Documentation','Codex']
KEYWORDS = {
    'combat':['combat','attack','damage','armor','weapon','backstab','critical'],
    'magic':['spell','mana','buff','debuff','resist','cast'],
    'progression':['xp','experience','level','class','race','skill','ability'],
    'npc':['npc','monster','mob','merchant','trainer','spawn'],
    'world':['room','exit','door','map','zone','movement','teleport'],
    'economy':['shop','gold','bank','repair','loot','treasure','buy','sell'],
    'quest':['quest','flag','reward','objective','stage'],
    'guild_social':['guild','party','clan','alignment','chat','social'],
    'dialogue_text':['dialogue','text','message','description','talk','say'],
    'scripting_events':['script','event','trigger','callback','timer']
}

def file_id(path: Path) -> str:
    rel = path.relative_to(ROOT).as_posix()
    return hashlib.sha1(rel.encode('utf-8')).hexdigest()[:16]

def classify(text: str, path: str) -> list[str]:
    hay = (path + '\n' + text[:5000]).lower()
    out=[]
    for name, words in KEYWORDS.items():
        if any(w in hay for w in words): out.append(name)
    return out or ['unclassified']

def read_text(path: Path) -> str:
    try: return path.read_text(encoding='utf-8', errors='replace')
    except Exception: return ''

def iter_files():
    for d in CANONICAL_DIRS:
        base=ROOT/d
        if not base.exists(): continue
        for p in base.rglob('*'):
            if p.is_file() and p.suffix.lower() in TEXT_EXT:
                yield p

def main():
    files=list(iter_files())
    conn=sqlite3.connect(DB)
    cur=conn.cursor()
    cur.executescript((Path(__file__).parent/'sdk_schema.sql').read_text())
    cur.execute('DELETE FROM records'); cur.execute('DELETE FROM relationships'); cur.execute('DELETE FROM evidence'); cur.execute('DELETE FROM build_manifest')
    nodes=[]; edges=[]; query_index={k:[] for k in list(KEYWORDS)+['unclassified']}; runtime_index={}; evidence_index={}
    for p in files:
        rel=p.relative_to(ROOT).as_posix(); txt=read_text(p); rid=file_id(p)
        labels=classify(txt, rel)
        title=rel.split('/')[-1]
        state='unknown'
        m=re.search(r'(?i)(Verified|Corroborated|Observed|Derived|Hypothesis|Gap|World Override|AI Proposal)', txt[:3000])
        if m: state=m.group(1)
        summary=' '.join(txt.strip().split())[:240]
        cur.execute('INSERT INTO records(id,type,name,source_path,evidence_state,confidence,summary) VALUES (?,?,?,?,?,?,?)', (rid, labels[0], title, rel, state, '', summary))
        nodes.append({'id':rid,'type':labels[0],'name':title,'source_path':rel,'evidence_state':state})
        for label in labels: query_index.setdefault(label,[]).append(rid)
        if rel.startswith('Evidence/'):
            eid='ev_'+rid
            cur.execute('INSERT INTO evidence(id,record_id,source,source_path,evidence_state,confidence,excerpt) VALUES (?,?,?,?,?,?,?)',(eid,rid,'repository',rel,state,'',summary))
            evidence_index[eid]={'record_id':rid,'source_path':rel,'state':state}
    # simple path containment relationships and subsystem bucket relationships
    for node in nodes:
        parts=node['source_path'].split('/')
        if len(parts)>2:
            parent='/'.join(parts[:-1])
            pid=hashlib.sha1(parent.encode()).hexdigest()[:16]
            edge={'source_id':pid,'relation':'contains','target_id':node['id'],'provenance':'path','confidence':'derived'}
            edges.append(edge)
            cur.execute('INSERT INTO relationships(source_id,relation,target_id,provenance,confidence) VALUES (?,?,?,?,?)',(edge['source_id'],edge['relation'],edge['target_id'],edge['provenance'],edge['confidence']))
    for k,v in [('built_at',datetime.now(timezone.utc).isoformat()),('file_count',str(len(files))),('node_count',str(len(nodes))),('edge_count',str(len(edges))),('canonical_dirs',json.dumps(CANONICAL_DIRS))]:
        cur.execute('INSERT INTO build_manifest(key,value) VALUES (?,?)',(k,v))
    conn.commit(); conn.close()
    (OUT/'core_graph.json').write_text(json.dumps({'nodes':nodes,'edges':edges},indent=2), encoding='utf-8')
    (OUT/'query_index.json').write_text(json.dumps(query_index,indent=2), encoding='utf-8')
    (OUT/'runtime_index.json').write_text(json.dumps(runtime_index,indent=2), encoding='utf-8')
    (OUT/'evidence_index.json').write_text(json.dumps(evidence_index,indent=2), encoding='utf-8')
    print(json.dumps({'status':'ok','database':str(DB),'nodes':len(nodes),'edges':len(edges),'files':len(files)}, indent=2))
if __name__=='__main__': main()
