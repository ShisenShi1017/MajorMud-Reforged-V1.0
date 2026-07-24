#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).resolve().parents[2]
d=json.loads((r/'Reports/WorldgroupNative/worldgroup_majorMud_session_callbacks_0008.json').read_text())
assert d['evidence_class']=='proven'
assert len(d['callbacks'])==3
roles={x['role'] for x in d['callbacks']}
assert roles=={'session_initialization','active_command_dispatch','session_transition_cleanup'}
a=next(x for x in d['callbacks'] if x['role']=='active_command_dispatch')
assert a['va']=='0x00405104'
assert a['local_callback_chain']['callback_array_global']=='0x0049D4D8'
assert a['local_callback_chain']['control_results']['-4'].startswith('continue')
assert d['command_boundary']['parser_fallback']=='0x004789CA after a local callback returns outside -2/-3/-4'
print('worldgroup MajorMUD session callbacks 0008: PASS')
