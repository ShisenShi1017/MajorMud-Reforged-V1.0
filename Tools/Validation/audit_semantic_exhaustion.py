#!/usr/bin/env python3
"""Truthful semantic-exhaustion audit for MajorMUD Reforged source families.

This audit deliberately separates parser success from gameplay-semantic completion.
It is read-only and writes a machine-readable report plus Markdown summary.
"""
from __future__ import annotations
import argparse, json
from pathlib import Path
from collections import Counter


def load_json(path: Path):
    with path.open('r', encoding='utf-8') as f:
        return json.load(f)


def records(doc):
    if isinstance(doc, list): return doc
    if isinstance(doc, dict):
        for key in ('records','rooms','exits','interactions','quests','flows','textblocks'):
            if isinstance(doc.get(key), list): return doc[key]
    return []


def has_text(obj, needles):
    text=json.dumps(obj, ensure_ascii=False, sort_keys=True).lower()
    return any(n in text for n in needles)


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--repo', default='.')
    ap.add_argument('--json-out', required=True)
    ap.add_argument('--md-out', required=True)
    args=ap.parse_args()
    root=Path(args.repo).resolve()

    paths={
      'mdb_rooms': root/'Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/rooms_decoded.json',
      'mdb_exits': root/'Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/exits_decoded.json',
      'mdb_interactions': root/'Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/room_interactions_decoded.json',
      'runtime_quests': root/'Data/Classic/quests.json',
      'runtime_triggers': root/'Data/Classic/triggers.json',
      'runtime_scripts': root/'Data/Classic/scripts.json',
    }
    missing=[str(p.relative_to(root)) for p in paths.values() if not p.exists()]
    if missing: raise SystemExit('Missing required inputs: '+', '.join(missing))

    rooms=records(load_json(paths['mdb_rooms']))
    exits=records(load_json(paths['mdb_exits']))
    interactions=records(load_json(paths['mdb_interactions']))
    quests=records(load_json(paths['runtime_quests']))
    triggers=records(load_json(paths['runtime_triggers']))
    scripts=records(load_json(paths['runtime_scripts']))

    qualified=[r for r in exits if str(r.get('qualifier','')).strip()]
    literal_unknown=[r for r in exits if 'unknown' in str(r.get('qualifier','')).lower()]
    semantic_incomplete_qualified=[r for r in qualified if r.get('runtime_disposition') in ('gate_evidence_preserved','unknown_requirements_not_invented') or not r.get('prerequisites')]

    qclasses=Counter(str(q.get('classification','unclassified')) for q in quests)
    executable=[q for q in quests if q.get('classification') in ('decoded_executable_quest','executable_quest','confirmed_executable_quest') or q.get('runtime_mapping',{}).get('status') in ('executable','runtime_ready')]
    pending=[q for q in quests if q.get('runtime_mapping',{}).get('status') == 'pending' or has_text(q,['pending-evidence-blocked'])]

    mp=list((root/'ImportedDatabases/MegaMudMP').glob('*.mp')) if (root/'ImportedDatabases/MegaMudMP').exists() else []
    native_names=['WCCMP001.DAT','WCCMP002.DAT','WBBMP001.DAT','WBBMP002.DAT','wccmmud.dll','wbbmmud.dll','wccmmpls.dll','wbbmmpls.dll','wccmmutl.exe','wbbmmutl.exe']
    native={name:[str(p.relative_to(root)) for p in root.rglob(name)] for name in native_names}
    omni=[p for p in root.rglob('*') if p.is_file() and 'omniverse' in str(p).lower()]
    redux=[p for p in root.rglob('*') if p.is_file() and 'redux' in str(p).lower()]

    report={
      'audit_id':'semantic-exhaustion-0181',
      'policy':{
        'parser_success_is_not_semantic_completion':True,
        'verified_runtime_allowed':True,
        'strongly_correlated_runtime_allowed':True,
        'strongly_correlated_requirements':['provenance','confidence','fallback','conflict tracking'],
        'no_invented_semantics':True,
      },
      'rooms':{'decoded':len(rooms),'parser_errors':0,'semantic_status':'not_exhausted'},
      'exits':{
        'decoded':len(exits),
        'qualified':len(qualified),
        'literal_unknown_qualifier_labels':len(literal_unknown),
        'qualified_records_without_exhaustive_semantics':len(semantic_incomplete_qualified),
        'semantic_status':'not_exhausted',
        'note':'A normalized qualifier label does not prove prerequisite evaluation semantics.'
      },
      'room_interactions':{'decoded':len(interactions),'semantic_prerequisites_complete':False},
      'triggers':{'generated_records':len(triggers),'records_requiring_requirement_semantics':sum(has_text(t,['unknown_requirements_not_invented']) or not t.get('prerequisites') for t in triggers),'semantic_status':'not_exhausted'},
      'quests':{'generated_records':len(quests),'classification_counts':dict(qclasses),'executable_records_detected':len(executable),'pending_records_detected':len(pending),'semantic_status':'not_exhausted'},
      'scripts':{'generated_records':len(scripts),'aggregate_only':len(scripts)<=1,'semantic_status':'not_exhausted'},
      'megamud_mp':{'file_count':len(mp),'byte_count':sum(p.stat().st_size for p in mp),'behavior_model_complete':False},
      'native_sources':native,
      'omniverse':{'matching_files':len(omni),'fully_reconciled':False},
      'redux':{'matching_files':len(redux),'fully_reconciled':False},
      'completion_gates':[
        'Complete room/exit prerequisite semantics and evaluation order',
        'Separate executable quests from components, gates, dialogue, evidence and false positives',
        'Decode textblock condition/action ordering, branches, flags, rewards, repeat/failure behavior',
        'Decode trigger ownership, prerequisites, timing, reset and chaining',
        'Expand scripts.json from aggregate evidence to decoded stock script records',
        'Interpret all MegaMUD .mp commands, loops, conditions, recovery and route reciprocity',
        'Reverse native WCC/WBB record layouts and runtime execution behavior',
        'Reconcile OmniVerse and Redux field-by-field without overriding primary evidence',
      ],
      'overall_status':'SEMANTICALLY_INCOMPLETE'
    }
    Path(args.json_out).write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    md=f"""# Semantic Exhaustion Audit 0181\n\n**Overall status: SEMANTICALLY INCOMPLETE**\n\nParser success and zero unknown opcodes do not constitute gameplay-semantic completion.\n\n## Verified Current Counts\n\n- Rooms decoded: {len(rooms):,}\n- Exits decoded: {len(exits):,}\n- Qualified exits: {len(qualified):,}\n- Literal `unknown` qualifier labels after normalization: {len(literal_unknown):,}\n- Qualified exits still lacking exhaustive prerequisite semantics: {len(semantic_incomplete_qualified):,}\n- Room interactions / generated triggers: {len(interactions):,} / {len(triggers):,}\n- Quest-family records: {len(quests):,}\n- Executable quest records detected by current runtime markers: {len(executable):,}\n- Pending quest-family records detected: {len(pending):,}\n- Generated script records: {len(scripts):,} (aggregate-only: {str(len(scripts)<=1).lower()})\n- MegaMUD `.mp` files: {len(mp):,} ({sum(p.stat().st_size for p in mp):,} bytes)\n- OmniVerse-matching files: {len(omni):,}\n- Redux-matching files: {len(redux):,}\n\n## Authority Rule\n\nVerified and strongly correlated findings may enter runtime. Strongly correlated findings must retain provenance, confidence, fallback behavior, and conflict tracking. Unresolved semantics must not be silently promoted merely because the source token was parsed.\n\n## Remaining Completion Gates\n\n""" + ''.join(f'- {x}\n' for x in report['completion_gates'])
    Path(args.md_out).write_text(md,encoding='utf-8')

if __name__=='__main__': main()
