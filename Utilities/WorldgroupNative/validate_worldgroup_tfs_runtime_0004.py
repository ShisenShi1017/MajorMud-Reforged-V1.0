#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
p=root/'Reports/WorldgroupNative/worldgroup_tfs_runtime_0004.json'
d=json.loads(p.read_text())
errors=[]
if d.get('wgserver_sha256')!='4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f': errors.append('hash')
if {x['value'] for x in d.get('states',[])} != set(range(5)): errors.append('states')
layout={x['symbol']:x for x in d.get('layout',[])}
if layout.get('_tfsfb_filename',{}).get('offset')!=16 or layout.get('_tfsfb_filename',{}).get('size')!=260: errors.append('find-layout')
if len(d.get('exports',[]))!=2: errors.append('exports')
print(json.dumps({'validator':'worldgroup_tfs_runtime_0004','errors':errors,'status':'pass' if not errors else 'fail'},indent=2))
sys.exit(bool(errors))
