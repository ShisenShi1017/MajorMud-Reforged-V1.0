#!/usr/bin/env python3
"""Promote strongly correlated semantics for MMExplorer room exit actions.

The MDB `Action...` room values encode accepted player-command alternatives for an
exit-bound physical/speech/item interaction. They are not treated as an ordered
macro. This pass preserves source commands, normalizes duplicates, identifies the
verb/object family, and records explicit unknowns for timing/state semantics.
"""
from __future__ import annotations
import argparse, json, re
from collections import Counter
from pathlib import Path

VERBS = {"push","pull","move","press","turn","twist","touch","lower","drop","use","aim","say","speak","river","hold","lift","spin","slide","rotate","rub","step","stand","jerk","place","destroy","answer","yank","dig","tear"}
SPEECH={"say","speak"}; ITEM={"use","aim"}; PHYSICAL=VERBS-SPEECH-ITEM-{"river","answer"}
DIRS={"north","south","east","west","northeast","northwest","southeast","southwest","up","down","n","s","e","w","ne","nw","se","sw","u","d"}

def load(path:Path): return json.loads(path.read_text(encoding='utf-8'))
def dump(path:Path,obj): path.parent.mkdir(parents=True,exist_ok=True); path.write_text(json.dumps(obj,indent=2,ensure_ascii=False)+"\n",encoding='utf-8')
def records(obj): return obj.get('records',obj) if isinstance(obj,dict) else obj

def parse_command(raw:str):
    s=' '.join(raw.strip().lower().split())
    item_id=None
    m=re.search(r'\s*\(item:\s*(\d+)\)\s*$',s)
    if m: item_id=int(m.group(1)); s=s[:m.start()].strip()
    parts=s.split(); verb=parts[0] if parts else ''
    if verb not in VERBS: verb='say' if len(parts)==1 else verb
    args=parts[1:] if parts and parts[0] in VERBS else parts
    direction=args[-1] if args and args[-1] in DIRS else None
    target=' '.join(args[:-1] if direction else args) or None
    family='speech' if verb in SPEECH or verb=='answer' or (len(parts)==1 and parts) else 'item_use' if verb in ITEM else 'physical_manipulation' if verb in PHYSICAL else 'unclassified_command'
    return {'command':raw,'normalized_command':s,'verb':verb,'target':target,'direction_argument':direction,'item_id':item_id,'command_family':family}

def enrich(rec):
    source_commands=rec.get('commands',[])
    message_template=bool(source_commands) and all('%s' in c for c in source_commands)
    parsed=[] if message_template else [parse_command(c) for c in source_commands]
    unique=[]; seen=set()
    for p in parsed:
        k=p['normalized_command']
        if k not in seen: seen.add(k); unique.append(p)
    families=sorted({p['command_family'] for p in unique})
    targets=sorted({p['target'] for p in unique if p['target']})
    item_ids=sorted({p['item_id'] for p in unique if p['item_id'] is not None})
    verbs=sorted({p['verb'] for p in unique if p['verb']})
    out=dict(rec)
    out['accepted_commands']=[p['normalized_command'] for p in unique]
    execution_model='native_passive_exit_action_no_player_command' if not source_commands else 'movement_message_template_not_activation_command' if message_template else 'any_one_command_alias_activates_interaction'
    out['command_semantics']={
      'execution_model':execution_model,
      'ordering':'alternatives_not_sequence',
      'binding':'exit_bound',
      'command_families':families,
      'verbs':verbs,'targets':targets,'required_item_ids':item_ids,
      'parsed_commands':unique,
      'activation_requirements':{'command_match':'required','source_room_match':'required','exit_direction_match':'required'},
      'state_effect':'native_action_result_not_yet_decoded',
      'repeatability':'not_yet_decoded','reset_timing':'not_yet_decoded','failure_message':'not_yet_decoded'
    }
    out['semantic_status']='strongly_correlated_command_activation' if unique else 'verified_non_command_exit_metadata'
    out['semantic_confidence']='strongly_correlated'
    out['semantic_provenance']=[
      'MMExplorer Rooms-table Action value binds commands to a named exit direction',
      'comma-separated variants contain abbreviations/synonyms and duplicates, supporting alias rather than sequence semantics'
    ]
    out['runtime_disposition']='command_alias_trigger_enabled_native_effect_pending' if unique else 'non_command_exit_metadata_not_registered_as_player_trigger'
    return out

def main():
 ap=argparse.ArgumentParser(); ap.add_argument('--root',default='.'); a=ap.parse_args(); root=Path(a.root)
 src=root/'Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/room_interactions_decoded.json'
 raw=load(src); enriched=[enrich(x) for x in records(raw)]
 dump(root/'Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0182/room_interactions_semantic.json',enriched)

 libp=root/'Librarium/Objects/triggers/mmexplorer_room_interaction_records_0140.json'; lib=load(libp)
 byid={x['id']:x for x in enriched}
 for r in lib['records']:
  e=byid[r['id']];
  for k in ('accepted_commands','command_semantics','semantic_status','semantic_confidence','semantic_provenance','runtime_disposition'): r[k]=e[k]
  r['validation']=[x for x in r.get('validation',[]) if x!='unknown_requirements_not_invented']+['command_alias_semantics_promoted_0182','native_effect_semantics_still_required']
 lib['code_update']='0182'; lib['semantic_enrichment_0182']={'status':'strongly_correlated_runtime_promotion','record_count':len(lib['records']),'execution_model':'any_one_command_alias_activates_interaction'}
 dump(libp,lib)

 for rel in ['Data/Classic/room_interactions.json','Data/Classic/triggers.json']:
  p=root/rel; obj=load(p); rr=records(obj)
  for r in rr:
   e=byid.get(r['id']);
   if not e: continue
   for k in ('accepted_commands','command_semantics','semantic_status','semantic_confidence','semantic_provenance','runtime_disposition'): r[k]=e[k]
   if rel.endswith('triggers.json'):
    r['validation']=[x for x in r.get('validation',[]) if x!='unknown_requirements_not_invented']+['command_alias_semantics_promoted_0182','native_effect_semantics_still_required']
  if isinstance(obj,dict): obj['compiler_code_update']='0182'; obj['semantic_enrichment']='room_interaction_command_aliases_0182'
  dump(p,obj)

 fam=Counter(tuple(x['command_semantics']['command_families']) for x in enriched)
 report={'code_update':'0182','records':len(enriched),'promoted_to_runtime':sum(bool(x['accepted_commands']) for x in enriched),'execution_model':'any_one_command_alias_activates_interaction','family_counts':{' + '.join(k):v for k,v in fam.items()},'unclassified_commands':sum('unclassified_command' in x['command_semantics']['command_families'] for x in enriched),'native_effect_semantics_pending':len(enriched),'remaining_unknowns':['state effect/open-close/teleport behavior','repeatability and reset timing','success/failure messages','persistent flag ownership and lifetime']}
 dump(root/'Reports/DataDecode/room_interaction_semantics_0182.json',report)
 md=f"""# Room Interaction Semantic Pass 0182\n\n- Exit-bound interaction records: **{len(enriched)}**\n- Runtime command activation promoted: **{report['promoted_to_runtime']}**\n- Unclassified command groups: **{report['unclassified_commands']}**\n- Execution model: **any one accepted alias activates the interaction; aliases are not an ordered macro**\n\nThis is strongly correlated from the MDB Action syntax, repeated synonym groups, abbreviations, and duplicate commands. Native action effects, repeatability, reset timing, flag lifetime, and success/failure messages remain pending binary/message correlation.\n"""
 (root/'Reports/DataDecode/room_interaction_semantics_0182.md').write_text(md,encoding='utf-8')
 print(json.dumps(report,indent=2))
if __name__=='__main__': main()
