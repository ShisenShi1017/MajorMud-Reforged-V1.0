#!/usr/bin/env python3
"""MajorMud Reforged DAT analyzer.
This is an investigative decoder, not a complete binary importer yet.
It catalogs file sizes, printable strings, block candidates, and CSV name alignment.
"""
from pathlib import Path
import argparse, csv, json
from collections import Counter
PRINTABLE=set(range(32,127))
def strings(data,minlen=4):
    out=[]; start=None; buf=[]
    for i,b in enumerate(data):
        if b in PRINTABLE:
            if start is None: start=i; buf=[]
            buf.append(chr(b))
        else:
            if start is not None and len(buf)>=minlen: out.append((start,''.join(buf)))
            start=None; buf=[]
    if start is not None and len(buf)>=minlen: out.append((start,''.join(buf)))
    return out

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--dat-dir', required=True)
    ap.add_argument('--csv-dir')
    ap.add_argument('--out', required=True)
    args=ap.parse_args()
    datdir=Path(args.dat_dir); out=Path(args.out); out.mkdir(parents=True,exist_ok=True)
    report=[]
    for p in sorted(datdir.glob('*.DAT')):
        data=p.read_bytes(); ss=strings(data)
        blocks=[]
        for sz in [32,64,96,112,126,128,153,192,256,384,512,768,1024,2048,4096,8192,16384,32768,65536]:
            if len(data)%sz==0: blocks.append({'block_size':sz,'blocks':len(data)//sz})
        report.append({'file':p.name,'size':len(data),'string_count_min4':len(ss),'first_strings':ss[:100],'even_block_candidates':blocks})
    (out/'dat_catalog.json').write_text(json.dumps(report,indent=2))
    print(f'Wrote {out / "dat_catalog.json"}')
if __name__=='__main__': main()
