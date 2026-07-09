#!/usr/bin/env python3
"""Query local Librarium distribution database.

Usage:
  python Tools/LibrariumDistribution/librarium_query.py search combat
  python Tools/LibrariumDistribution/librarium_query.py record <record_id>
  python Tools/LibrariumDistribution/librarium_query.py refs <record_id>
"""
from __future__ import annotations
import sqlite3, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DB=ROOT/'Generated'/'Librarium'/'librarium.sqlite'

def connect():
    if not DB.exists():
        raise SystemExit('Missing Generated/Librarium/librarium.sqlite. Run build_librarium_distribution.py first.')
    return sqlite3.connect(DB)

def search(term):
    q=f'%{term.lower()}%'
    with connect() as c:
        for row in c.execute("SELECT id,type,name,source_path,evidence_state FROM records WHERE lower(name) LIKE ? OR lower(source_path) LIKE ? OR lower(summary) LIKE ? LIMIT 50", (q,q,q)):
            print('\t'.join(str(x) for x in row))

def record(rid):
    with connect() as c:
        row=c.execute('SELECT id,type,name,source_path,evidence_state,confidence,summary FROM records WHERE id=?',(rid,)).fetchone()
        print(row if row else 'not found')

def refs(rid):
    with connect() as c:
        print('OUTGOING')
        for row in c.execute('SELECT relation,target_id,provenance,confidence FROM relationships WHERE source_id=? LIMIT 100',(rid,)): print(row)
        print('INCOMING')
        for row in c.execute('SELECT source_id,relation,provenance,confidence FROM relationships WHERE target_id=? LIMIT 100',(rid,)): print(row)

def main():
    if len(sys.argv)<3: raise SystemExit(__doc__)
    cmd,arg=sys.argv[1],sys.argv[2]
    {'search':search,'record':record,'refs':refs}.get(cmd, lambda x: (_ for _ in ()).throw(SystemExit(__doc__)))(arg)
if __name__=='__main__': main()
