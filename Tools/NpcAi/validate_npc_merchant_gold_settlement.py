#!/usr/bin/env python3
from pathlib import Path
import argparse,json
p=argparse.ArgumentParser();p.add_argument('--root',default='.');a=p.parse_args();r=Path(a.root)
cs=(r/'Apps/Source/Reforged.Server/RuntimeNpcMerchantGoldSettlement.cs').read_text()
for token in ['SettleExact','DebitCarried','CreditCarried','AdjustNpcGoldExact','CompleteNpcService','actor.Restore(actorBefore)','active-request-mismatch','amount-must-be-positive']:
    assert token in cs, token
for rel in ['Evidence/NpcAI/npc_ai_merchant_gold_settlement_evidence.json','Evidence/NpcAI/npc_ai_merchant_gold_settlement_provenance.json','Generated/NPCReconstruction/npc_ai_runtime_catalog.json','Data/Classic/npc_ai_runtime.json']:
    json.loads((r/rel).read_text())
cat=json.loads((r/'Generated/NPCReconstruction/npc_ai_runtime_catalog.json').read_text())
assert cat['schema_version']=='npc-ai-catalog.v51'
assert 'npc_merchant_gold_settlement' in cat['ownership_catalog']
assert any('merchant gold settlement' in x for x in cat['decision_runtime']['active'])
print(json.dumps({'passed':True,'schema':cat['schema_version'],'npc_count':cat['npc_count'],'spawn_count':cat['spawn_count']}))
