#!/usr/bin/env python3
import csv, json, re, sys
from pathlib import Path

repo=Path('/mnt/data/sr_work/MajorMud-Reforged-V1.0-main')
rooms=json.load(open(repo/'Data/rooms.json'))
by_name={}
for r in rooms:
    by_name.setdefault(r.get('name','').strip(), []).append(r)
roomnum_to_name={r['roomNumber']:r['name'].strip() for r in rooms if 'roomNumber' in r}

dir_map={'north':'N','south':'S','east':'E','west':'W','up':'U','down':'D','n':'N','s':'S','e':'E','w':'W','u':'U','d':'D'}

runtime_files=sorted(Path('/mnt/data').glob('Pasted text*.txt'))
follow_re=re.compile(r'-- Following your Party leader ([a-z]+) --', re.I)
# lines that look like room title after movement; skip known non-room lines
skip_prefixes=('Also here:', 'Obvious exits:', 'The room is', '[HP=', 'You ', 'Azriel ', 'Jarlaxle ', 'Shisenshi ', '*', '>>', 'Attempting', 'Sneaking', 'The following', '  ')

def is_room_line(s):
    s=s.strip()
    if not s or s.startswith(skip_prefixes): return False
    if ':' in s and s.startswith('['): return False
    # room names often title case, may include comma
    return any(c.isalpha() for c in s) and len(s) < 80


def canonical_allows(prev_name, direction, next_name):
    prevs=by_name.get(prev_name, [])
    next_names=set([next_name])
    if not prevs:
        return 'REVIEW', 'previous_room_name_not_in_canonical'
    if next_name not in by_name:
        return 'REVIEW', 'next_room_name_not_in_canonical'
    found_dir=False
    for r in prevs:
        for e in r.get('exits',[]):
            if e.get('dir')==direction:
                found_dir=True
                dest=e.get('to_room')
                if roomnum_to_name.get(dest)==next_name:
                    return 'PASS', 'canonical_exit_matches_name'
    if found_dir:
        return 'REVIEW', 'direction_exists_but_destination_name_not_matched_ambiguous_or_special'
    return 'REVIEW', 'direction_not_found_for_any_canonical_room_with_name'

rows=[]
for fp in runtime_files:
    lines=fp.read_text(errors='ignore').splitlines()
    current_room=None
    i=0
    while i < len(lines):
        line=lines[i].strip()
        if is_room_line(line) and line in by_name:
            current_room=line
        m=follow_re.search(line)
        if m:
            raw=m.group(1).lower()
            d=dir_map.get(raw, raw.upper())
            # find next canonical-looking room line in next few lines
            next_room=None
            for j in range(i+1, min(i+8,len(lines))):
                cand=lines[j].strip()
                if is_room_line(cand):
                    next_room=cand
                    break
            status,reason=canonical_allows(current_room, d, next_room) if current_room and next_room else ('REVIEW','missing_previous_or_next_room_context')
            rows.append({
                'runtime_file':fp.name,
                'line_number':i+1,
                'previous_room':current_room or '',
                'direction_text':raw,
                'canonical_direction':d,
                'next_room':next_room or '',
                'status':status,
                'reason':reason,
                'runtime_line':line,
            })
        i+=1

out=Path('/mnt/data/pass0013/runtime_follow_movement_validation.csv')
with out.open('w', newline='') as f:
    w=csv.DictWriter(f, fieldnames=list(rows[0].keys()) if rows else ['status'])
    w.writeheader(); w.writerows(rows)
from collections import Counter
summary={
    'validation':'ServerRuntimeValidation0013',
    'scope':'movement-based party follow transitions against canonical /Data room exits',
    'runtime_files':[p.name for p in runtime_files],
    'total_follow_events':len(rows),
    'status_counts':Counter(r['status'] for r in rows),
    'reason_counts':Counter(r['reason'] for r in rows),
    'canonical_data_changed':False,
    'promotion_decision':'No canonical /Data promotion. Evidence only; REVIEW rows are ambiguity/special-exit candidates requiring stronger source confirmation.',
}
# convert counters
summary['status_counts']=dict(summary['status_counts'])
summary['reason_counts']=dict(summary['reason_counts'])
Path('/mnt/data/pass0013/runtime_follow_movement_summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps(summary, indent=2))
