#!/usr/bin/env python3
import json, sys, os
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
graph=json.load(open(os.path.join(root,'Graph','core_graph.json'),encoding='utf-8'))
q=' '.join(sys.argv[1:]).lower() if len(sys.argv)>1 else ''
if not q:
    print('Usage: python Librarium/Tools/librarium_query.py <term>')
    raise SystemExit(0)
nodes=[n for n in graph['nodes'] if q in str(n.get('name','')).lower() or q in str(n.get('nodeId','')).lower()]
ids={n['nodeId'] for n in nodes}
edges=[e for e in graph['edges'] if e['source'] in ids or e['target'] in ids or q in e.get('relationship','').lower()]
print(json.dumps({'query':q,'nodes':nodes[:50],'relationships':edges[:100],'nodeMatches':len(nodes),'relationshipMatches':len(edges)},indent=2))
