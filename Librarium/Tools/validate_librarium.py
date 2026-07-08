#!/usr/bin/env python3
import json, os, sys, collections
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
g=json.load(open(os.path.join(root,'Graph','core_graph.json'),encoding='utf-8'))
node_ids={n['nodeId'] for n in g['nodes']}
missing_src=[e for e in g['edges'] if e['source'] not in node_ids]
missing_tgt=[e for e in g['edges'] if e['target'] not in node_ids]
dups=collections.Counter((e['source'],e['relationship'],e['target'],json.dumps(e.get('properties',{}),sort_keys=True)) for e in g['edges'])
dupe_count=sum(1 for v in dups.values() if v>1)
status='PASS' if not missing_src and not missing_tgt and not dupe_count else 'FAIL'
print(json.dumps({'status':status,'nodes':len(g['nodes']),'relationships':len(g['edges']),'missingSources':len(missing_src),'missingTargets':len(missing_tgt),'duplicateEdges':dupe_count},indent=2))
sys.exit(0 if status=='PASS' else 1)
