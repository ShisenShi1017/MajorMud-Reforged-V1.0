import json,pathlib
root=pathlib.Path(__file__).parents[2]
d=json.loads((root/'Librarium/Evidence/native_host_import_reference_0005.json').read_text())
assert d['schema']=='majorMud.native-host-import-reference.v1'
assert len(d['modules'])==4
for m in d['modules']:
 assert sum(m['counts'].values())==len(m['imports'])
 assert m['counts']['direct_callable_import']>0
assert any(x['cross_distribution_classification']=='iat_data_or_indirect_reference' for x in d['symbols'])
assert any(x['cross_distribution_classification']=='imported_no_static_code_reference' for x in d['symbols'])
print('native host import reference validation passed')
