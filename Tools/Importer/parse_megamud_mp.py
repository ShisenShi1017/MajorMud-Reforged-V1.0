#!/usr/bin/env python3
"""MegaMud .mp parser for MajorMud Reforged.
Runs without Access/OLEDB. It converts .mp files into JSON evidence records.
Usage: python parse_megamud_mp.py <mp-folder> <rooms-csv> <output-folder>
"""
import csv, json, re, sys, hashlib
from pathlib import Path

def norm(s): return re.sub(r'[^a-z0-9]+',' ',(s or '').lower()).strip()

def load_rooms(path):
    rooms_by_key={}; rooms_by_name={}
    with open(path, newline='', encoding='utf-8-sig', errors='replace') as f:
        for r in csv.DictReader(f):
            try: key=(int(r.get('Map Number') or 0), int(r.get('Room Number') or 0))
            except: continue
            rec={'mapNumber':key[0],'roomNumber':key[1],'name':(r.get('Name') or '').strip()}
            rooms_by_key[key]=rec; rooms_by_name.setdefault(norm(rec['name']),[]).append(rec)
    return rooms_by_key, rooms_by_name

def parse(mp_dir, rooms_csv, out_dir):
    mp_dir=Path(mp_dir); out_dir=Path(out_dir); out_dir.mkdir(parents=True, exist_ok=True)
    rooms_by_key, rooms_by_name=load_rooms(rooms_csv)
    room_re=re.compile(r'^(?P<code>[^:]*):(?P<area>[^:]*):(?P<label>.*?)(?:-+(?P<map>\d+)\s+(?P<room>\d+))?$')
    summary={'mpFiles':0,'roomHeaders':0,'csvMatches':0}
    for p in sorted(mp_dir.glob('*.mp')):
        lines=[ln.strip() for ln in p.read_text(errors='replace').replace('\r','').split('\n') if ln.strip()]
        rec={'file':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'roomHeaders':[],'routeLines':[]}
        for ln in lines:
            if ln.startswith('[') and ln.endswith(']') and ':' in ln:
                m=room_re.match(ln[1:-1])
                if m:
                    gd=m.groupdict(); mp=rn=None
                    if gd.get('map') and gd.get('room'): mp=int(gd['map']); rn=int(gd['room'])
                    h={'code':gd.get('code') or '', 'area':gd.get('area') or '', 'label':(gd.get('label') or '').strip(), 'mapNumber':mp, 'roomNumber':rn, 'raw':ln, 'evidence':[]}
                    if mp is not None and rn is not None and (mp,rn) in rooms_by_key:
                        h['csvMatch']=rooms_by_key[(mp,rn)]; h['evidence'].append('rooms.TXT map/room match'); summary['csvMatches']+=1
                    rec['roomHeaders'].append(h); summary['roomHeaders']+=1
            elif ':' in ln:
                rec['routeLines'].append({'raw':ln,'parts':ln.split(':')})
        (out_dir/(p.stem+'.json')).write_text(json.dumps(rec, indent=2, ensure_ascii=False), encoding='utf-8')
        summary['mpFiles']+=1
    (out_dir/'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
if __name__ == '__main__':
    if len(sys.argv)!=4:
        print(__doc__); sys.exit(2)
    parse(sys.argv[1], sys.argv[2], sys.argv[3])
