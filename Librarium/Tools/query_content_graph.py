#!/usr/bin/env python3
import json,sys,pathlib
root=pathlib.Path(__file__).resolve().parents[2]
g=json.load(open(root/"Librarium"/"Graph"/"core_graph.json",encoding="utf-8"))
q=" ".join(sys.argv[1:]).lower()
if not q: print("Usage: python Librarium/Tools/query_content_graph.py <term>"); raise SystemExit(0)
for n in g["nodes"]:
    h=(n.get("nodeId","")+" "+n.get("name","")+" "+" ".join(map(str,n.get("notes",[])))).lower()
    if q in h: print(json.dumps({"nodeId":n.get("nodeId"),"type":n.get("type"),"name":n.get("name"),"evidenceState":n.get("evidenceState"),"confidence":n.get("confidence")},ensure_ascii=False))
