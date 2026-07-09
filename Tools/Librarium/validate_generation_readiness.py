#!/usr/bin/env python3
"""Validate CodeUpdate0125 Librarium generation readiness contracts.
Does not generate /Data and does not import runtime/server code.
"""
import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
contracts_path=root/'Librarium'/'GenerationReadiness'/'generation_contracts_0125.json'
if not contracts_path.exists():
    print('FAIL: missing generation_contracts_0125.json')
    sys.exit(1)
contracts=json.loads(contracts_path.read_text()).get('contracts',[])
fail=[]
for c in contracts:
    fam=c.get('object_family')
    cat=root/c.get('source_catalog','')
    exp=c.get('export_contract',{})
    if not fam or not cat.exists(): fail.append(f'{fam}: missing catalog')
    for k in ['runtime_destination','serialization','world_inheritance','override_behavior','runtime_consumer','generation_order','dependencies','required_fields','validation_rules']:
        if k not in exp: fail.append(f'{fam}: missing {k}')
    for d in exp.get('dependencies',[]):
        if d not in [x.get('object_family') for x in contracts]: fail.append(f'{fam}: dependency {d} missing')
print(json.dumps({'status':'FAIL' if fail else 'PASS','object_families':len(contracts),'failures':fail},indent=2))
sys.exit(1 if fail else 0)
