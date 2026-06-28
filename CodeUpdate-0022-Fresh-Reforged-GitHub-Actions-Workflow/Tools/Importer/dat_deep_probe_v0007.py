#!/usr/bin/env python3
"""
Reforged DAT Deep Probe v0007
Runs without Access/OLEDB. Produces catalogs and CSV-name offset reports.
This is an analysis tool; it does not modify source .DAT files.
"""
import csv, json, hashlib, sys
from pathlib import Path
from collections import Counter

def sha256(path):
    h=hashlib.sha256(); h.update(path.read_bytes()); return h.hexdigest()

def ascii_runs(data, minlen=4):
    out=[]; i=0
    while i<len(data):
        if 32<=data[i]<=126:
            j=i
            while j<len(data) and 32<=data[j]<=126: j+=1
            if j-i>=minlen: out.append((i, data[i:j].decode('latin1')))
            i=j
        else: i+=1
    return out

def read_csv(path):
    with path.open(newline='', encoding='utf-8', errors='replace') as f:
        return list(csv.DictReader(f))

def main(native_dir, csv_dir, out_dir):
    native=Path(native_dir); csvd=Path(csv_dir); out=Path(out_dir); out.mkdir(parents=True, exist_ok=True)
    catalog={}
    for p in sorted(native.glob('*.DAT')):
        data=p.read_bytes(); strings=ascii_runs(data)
        catalog[p.name]={
            'size':len(data), 'sha256':sha256(p), 'ascii_string_count_min4':len(strings),
            'candidate_block_sizes':[{'block_size':s,'slots':len(data)//s} for s in [16,32,64,96,126,128,153,192,256,384,512,768,1024,2048,4096,8192,16384,32768,65536] if len(data)%s==0],
            'first_strings':strings[:50]
        }
    (out/'file_catalog.json').write_text(json.dumps(catalog, indent=2), encoding='utf-8')
    print('Wrote', out/'file_catalog.json')

if __name__ == '__main__':
    if len(sys.argv)!=4:
        print('Usage: dat_deep_probe_v0007.py <native_dat_dir> <csv_export_dir> <output_dir>')
        sys.exit(2)
    main(sys.argv[1], sys.argv[2], sys.argv[3])
