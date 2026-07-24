from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['RuntimeNpcCorpseGoldResult','ReconcileNpcCorpseGold','NPC corpse-gold reconciliation','DeterministicLootValue']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['npc-corpse-gold-deterministic-idempotent-and-conflict-safe','deterministic NPC corpse gold changed outcome','conflicting NPC corpse gold was overwritten','invalid NPC corpse gold range was accepted']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0195/item_npc_corpse_gold_semantics.json','Evidence/Items/CodeUpdate0195/provenance.json','Librarium/Policies/runtime_item_npc_corpse_gold_policy.json']:
    json.loads((root/rel).read_text())
items=json.loads((root/'Data/Classic/items.json').read_text())['records']
if len(items)!=1950: raise SystemExit(f'unexpected canonical item count: {len(items)}')
print(f'CodeUpdate0195 validation passed; canonical_items={len(items)}')
