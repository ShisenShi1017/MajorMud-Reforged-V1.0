#!/usr/bin/env python3
"""Minimal Librarium query engine over local generated indexes.
Examples:
  python Librarium/Tools/query_engine_0013.py --contains "Ability 123"
  python Librarium/Tools/query_engine_0013.py --predicate needs_evidence
"""
import json, argparse
from pathlib import Path

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--contains')
    ap.add_argument('--predicate')
    ap.add_argument('--limit', type=int, default=50)
    args=ap.parse_args()
    graph=json.loads((Path(args.root)/'Generated/LibrariumLocal/core_graph.json').read_text(encoding='utf-8'))
    results=[]
    if args.contains:
        q=args.contains.lower()
        for n in graph.get('nodes',[]):
            if q in json.dumps(n).lower(): results.append({'kind':'node','record':n})
        for e in graph.get('edges',[]):
            if q in json.dumps(e).lower(): results.append({'kind':'edge','record':e})
    if args.predicate:
        for e in graph.get('edges',[]):
            if e.get('predicate') == args.predicate: results.append({'kind':'edge','record':e})
    print(json.dumps({'count':len(results),'results':results[:args.limit]}, indent=2))
if __name__ == '__main__': main()
