from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['RuntimeItemOrphanReconciliationResult','ReconcileMissingContainerParents','MISSING_CONTAINER_PARENT','MISSING_CORPSE_PARENT','recovered_from_missing_container_id','recovery_policy']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['missing-container-parent-reconciliation','missing-corpse-parent-validation','ownerless orphan was silently recovered','orphan recovery provenance was not persisted']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0197/item_orphan_parent_reconciliation_semantics.json','Evidence/Items/CodeUpdate0197/provenance.json','Librarium/Policies/runtime_item_orphan_parent_reconciliation_policy.json']:
    json.loads((root/rel).read_text())
items=json.loads((root/'Data/Classic/items.json').read_text())['records']
if len(items)!=1950: raise SystemExit(f'unexpected canonical item count: {len(items)}')
print(f'CodeUpdate0197 validation passed; canonical_items={len(items)}')
