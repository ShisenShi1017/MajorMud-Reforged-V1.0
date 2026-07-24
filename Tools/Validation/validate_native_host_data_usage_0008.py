import json, pathlib
root=pathlib.Path(__file__).parents[2]
d=json.loads((root/'Librarium/Evidence/native_host_data_usage_0008.json').read_text())
assert d['schema']=='majormud.native-host-data-usage.v1'
assert d['update']=='FinalNativeClosure0008'
assert len(d['modules'])==4
assert len(d['symbols'])>=34
for m in d['modules']:
    assert sum(m['counts'].values())==len(m['imports'])
    assert m['counts'].get('import_slot_resolves_to_pointer_with_observed_client_dereferences',0)>0
paired=[('wccmmud.dll','wbbmmud.dll'),('wccmmpls.dll','wbbmmpls.dll')]
for left,right in paired:
    a=next(x for x in d['modules'] if x['file']==left)
    b=next(x for x in d['modules'] if x['file']==right)
    ax={x['symbol']:(x['semantic_classification_0008'],x['static_reference_count'],x['pointer_dereference_count'],x['observed_client_offsets'],x['observed_access_widths']) for x in a['imports']}
    bx={x['symbol']:(x['semantic_classification_0008'],x['static_reference_count'],x['pointer_dereference_count'],x['observed_client_offsets'],x['observed_access_widths']) for x in b['imports']}
    assert ax==bx
assert any(x['symbol']=='_input' and 60 in x['observed_client_offsets'] for x in d['symbols'])
assert any(x['symbol']=='_margv' and set(x['observed_client_offsets']) >= {0,4,8,12,16} for x in d['symbols'])
print('native host data usage validation passed')
