#!/usr/bin/env python3
import json, sys
from pathlib import Path
root = Path(__file__).resolve().parents[2]
graph = json.load(open(root / "Librarium/Graph/core_graph.json", encoding="utf-8"))
q = sys.argv[1].lower() if len(sys.argv)>1 else "runtime:"
nodes = {n["nodeId"]: n for n in graph["nodes"]}
for n in graph["nodes"]:
    if q in n.get("nodeId","").lower() or q in n.get("name","").lower():
        print(f"{n['nodeId']} :: {n.get('name')}")
        incoming=[e for e in graph["edges"] if e.get("target")==n["nodeId"]][:20]
        outgoing=[e for e in graph["edges"] if e.get("source")==n["nodeId"]][:20]
        print("  incoming:", len([e for e in graph['edges'] if e.get('target')==n['nodeId']]))
        for e in incoming[:5]: print("   <-", e.get('type'), e.get('source'))
        print("  outgoing:", len([e for e in graph['edges'] if e.get('source')==n['nodeId']]))
        for e in outgoing[:5]: print("   ->", e.get('type'), e.get('target'))
        print()
