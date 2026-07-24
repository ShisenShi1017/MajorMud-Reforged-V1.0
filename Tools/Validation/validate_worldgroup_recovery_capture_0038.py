#!/usr/bin/env python3
import json, pathlib, subprocess, sys, tempfile
root=pathlib.Path(__file__).resolve().parents[2]
e=json.loads((root/'Librarium/Evidence/worldgroup_recovery_capture_harness_0038.json').read_text())
assert e['status']=='implemented_evidence_capture_harness'
assert e['safety_policy']['runtime_behavior_changed'] is False
assert e['safety_policy']['unknown_format_parser_activated'] is False
tool=root/'Tools/Worldgroup/capture_btrieve_recovery_artifacts.py'
with tempfile.TemporaryDirectory() as td:
    td=pathlib.Path(td); source=td/'source'; source.mkdir(); out=td/'out'
    (source/'BTRVLOG.TRN').write_bytes(b'abc')
    subprocess.run([sys.executable,str(tool),'snapshot',str(source),str(out),'--label','before'],check=True,capture_output=True)
    (source/'BTRVLOG.TRN').write_bytes(b'abcd'); (source/'BTRVLOG.RCV').write_bytes(b'x')
    subprocess.run([sys.executable,str(tool),'snapshot',str(source),str(out),'--label','after'],check=True,capture_output=True)
    diff=td/'diff.json'
    subprocess.run([sys.executable,str(tool),'diff',str(out/'capture-before.json'),str(out/'capture-after.json'),str(diff)],check=True,capture_output=True)
    d=json.loads(diff.read_text()); assert d['summary']=={'created':1,'deleted':0,'changed':1,'unchanged':0}
print('Worldgroup Update0038 recovery capture harness validated')
