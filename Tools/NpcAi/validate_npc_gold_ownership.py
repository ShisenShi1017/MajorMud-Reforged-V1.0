#!/usr/bin/env python3
from pathlib import Path
import argparse,json
p=argparse.ArgumentParser();p.add_argument('--root',default='.');a=p.parse_args();r=Path(a.root)
cs=(r/'Apps/Source/Reforged.Server/RuntimeNpcGoldOwnership.cs').read_text()
for token in ['InspectNpcGold','SetNpcGoldExact','AdjustNpcGoldExact','checked(previous + delta)','insufficient-npc-gold','gold-must-be-nonnegative','Save();']:
    assert token in cs, token
for rel in ['Evidence/NpcAI/npc_ai_gold_ownership_evidence.json','Evidence/NpcAI/npc_ai_gold_ownership_provenance.json','Generated/NPCReconstruction/npc_ai_runtime_catalog.json','Data/Classic/npc_ai_runtime.json']:
    json.loads((r/rel).read_text())
cat=json.loads((r/'Generated/NPCReconstruction/npc_ai_runtime_catalog.json').read_text())
assert cat['schema_version']=='npc-ai-catalog.v50'
assert 'npc_gold' in cat['ownership_catalog']
print(json.dumps({'passed':True,'schema':cat['schema_version'],'npc_count':cat['npc_count'],'spawn_count':cat['spawn_count']}))
