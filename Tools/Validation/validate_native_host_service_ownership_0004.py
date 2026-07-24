import json,pathlib
root=pathlib.Path(__file__).parents[2]
d=json.loads((root/'Librarium/Evidence/native_host_service_ownership_0004.json').read_text())
assert d['schema']=='majorMud.native-host-service-ownership.v1'
assert len(d['symbols'])>=149
by={x['domain']:x for x in d['domains']}
for k in ['host_persistence_dfa','host_randomness','host_clock_calendar','host_polling_scheduler','host_message_rendering_transport','host_input_parser','host_session_user_state']:
 assert k in by and by[k]['symbol_count']>0
sy={x['symbol']:x['domain'] for x in d['symbols']}
assert sy['_lngrnd']=='host_randomness' and sy['_genrdn']=='host_randomness'
assert sy['_dfaAcqLock']=='host_persistence_dfa'
assert sy['_prf']=='host_message_rendering_transport'
print('native host service ownership validation passed')
