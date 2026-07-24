#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
p=root/'Reports/WorldgroupNative/worldgroup_prng_runtime_0003.json'
d=json.loads(p.read_text())
errors=[]
if d['algorithm']['seed_update'] != 'seed = (seed * 0x015A4E35 + 1) mod 2^32': errors.append('algorithm')
if d['algorithm']['range'] != '0..32767': errors.append('range')
if d['seed_lifecycle']['static_srand_calls_in_wgserver'] != 1: errors.append('srand-calls')
if d['seed_lifecycle']['resolution_seconds'] != 2: errors.append('resolution')
print('PASS' if not errors else 'FAIL: '+','.join(errors))
sys.exit(bool(errors))
