#!/usr/bin/env python3
"""Query CodeUpdate 0078 host timing artifacts."""
import json, pathlib, sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
files=[ROOT/'Librarium/HostExecutionModel/host_execution_model_0078.json',ROOT/'Librarium/HostExecutionModel/world_loop_reconstruction_0078.json',ROOT/'Librarium/BehaviorTimelines/subsystem_behavior_timelines_0078.json',ROOT/'Librarium/GapRegistry/host_timing_gaps_0078.json']
q=' '.join(sys.argv[1:]).lower()
if not q:
    print('Usage: query_host_timing_0078.py <term>')
    sys.exit(1)
for p in files:
    data=json.loads(p.read_text())
    text=json.dumps(data,indent=2).lower()
    if q in text:
        print(f'--- {p.relative_to(ROOT)} ---')
        print(json.dumps(data,indent=2)[:4000])
