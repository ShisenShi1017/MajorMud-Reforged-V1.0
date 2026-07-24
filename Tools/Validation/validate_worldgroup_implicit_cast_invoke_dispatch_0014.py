#!/usr/bin/env python3
import json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
for p in [root/'Reports/WorldgroupNative/worldgroup_implicit_cast_invoke_dispatch_0014.json', root/'Librarium/Evidence/worldgroup_implicit_cast_invoke_dispatch_0014.json']:
    d=json.loads(p.read_text(encoding='utf-8'))
    assert d['gameplay_branch']['matcher']=='0x0046ABB4'
    assert d['gameplay_branch']['shared_executor']=='0x00461273'
    assert d['structure_bindings']['selector_0x6C']=='INVOKE power path'
    assert d['structure_bindings']['selector_0x1F']=='CAST spell path'
    assert len(d['proven_findings'])>=10
cs=(root/'Apps/Source/Reforged.Server/WorldgroupImplicitCastInvokeDispatchRegistry.cs').read_text(encoding='utf-8')
for token in ['PlayerClassRecordIdOffset = 0x92','ClassTypeOffset = 0x40','KaiClassType = 5','CastSelector = 0x1F','InvokeSelector = 0x6C']:
    assert token in cs
print('worldgroup implicit cast/invoke dispatch 0014: PASS')
