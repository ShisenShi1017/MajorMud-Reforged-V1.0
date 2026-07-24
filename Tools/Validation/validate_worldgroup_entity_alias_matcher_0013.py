#!/usr/bin/env python3
import json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
for p in [root/'Reports/WorldgroupNative/worldgroup_entity_alias_matcher_0013.json', root/'Librarium/Evidence/worldgroup_entity_alias_matcher_0013.json']:
    d=json.loads(p.read_text(encoding='utf-8'))
    assert d['function']['va']=='0x0046ABB4'
    assert d['function']['argument_count']==5
    assert len(d['proven_findings'])>=10
    assert d['classification']['proven']=='bounded player-scoped entity/alias matcher'
cs=(root/'Apps/Source/Reforged.Server/WorldgroupEntityAliasMatcherRegistry.cs').read_text(encoding='utf-8')
assert 'MaximumCandidateIds = 100' in cs
assert 'MaximumPasses = 50' in cs
assert 'IsCommandTableLookup => false' in cs
print('worldgroup entity alias matcher 0013: PASS')
