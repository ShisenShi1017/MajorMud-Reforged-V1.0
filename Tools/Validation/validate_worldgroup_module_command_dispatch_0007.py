#!/usr/bin/env python3
import json, pathlib
r=pathlib.Path(__file__).resolve().parents[2]
d=json.loads((r/'Reports/WorldgroupNative/worldgroup_module_command_dispatch_0007.json').read_text())
assert d['evidence_class']=='proven'
assert d['global_command_registry']['capacity']==50
assert d['register_module']['statistics_stride_bytes']==36
assert len(d['majorMud_binding']['WCCMMUD.DLL']['descriptor_callbacks'])==9
assert d['module_local_dispatch']['selection'][0]['callback_offset']=='0x1C'
assert d['module_local_dispatch']['selection'][1]['callback_offset']=='0x2C'
assert d['module_local_dispatch']['selection'][2]['callback_offset']=='0x20'
print('worldgroup module-command dispatch 0007: PASS')
