from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['RuntimeItemDuplicateReconciliationResult','RuntimeItemStateReconciler','ReconcileExactDuplicates','ReconcileExactDuplicateRecords','Conflicting duplicate item instance ID']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['exact-duplicate-item-and-corpse-reconciliation','exact duplicate reconciliation did not collapse duplicate records','conflicting duplicate item instance was silently reconciled']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0196/item_exact_duplicate_reconciliation_semantics.json','Evidence/Items/CodeUpdate0196/provenance.json','Librarium/Policies/runtime_item_exact_duplicate_reconciliation_policy.json']:
    json.loads((root/rel).read_text())
items=json.loads((root/'Data/Classic/items.json').read_text())['records']
if len(items)!=1950: raise SystemExit(f'unexpected canonical item count: {len(items)}')
print(f'CodeUpdate0196 validation passed; canonical_items={len(items)}')
