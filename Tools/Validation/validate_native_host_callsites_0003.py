import json, pathlib
p=pathlib.Path(__file__).parents[2]/'Librarium/Evidence/native_host_callsite_map_0003.json'
d=json.loads(p.read_text())
assert d['schema']=='majorMud.native-host-callsite-map.v1'
assert len(d['modules'])==4
assert all(m['wgserver_import_count']>40 for m in d['modules'])
assert all(m['wgserver_thunk_count']==m['wgserver_import_count'] for m in d['modules'])
assert any(f['name']=='_lngrnd' for f in d['functions'])
print('native host callsite validation passed')
