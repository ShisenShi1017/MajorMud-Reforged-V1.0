from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['RuntimeItemOwnerLocationReconciliationResult','ReconcileOwnerLocationState','OWNER_LOCATION_ID_MISMATCH','NONOWNED_LOCATION_HAS_OWNER','NON_EQUIPMENT_HAS_SLOT','EQUIPMENT_MISSING_SLOT','owner_location_recovery']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['owner-location-state-reconciliation','owned location identity was not normalized','room-held stale owner was not cleared','slotless equipment was not recovered carried']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0198/item_owner_location_reconciliation_semantics.json','Evidence/Items/CodeUpdate0198/provenance.json','Librarium/Policies/runtime_item_owner_location_reconciliation_policy.json']:
    json.loads((root/rel).read_text())
items_path=root/'Data/Classic/items.json'
if items_path.exists():
    items=json.loads(items_path.read_text())['records']
    if len(items)!=1950: raise SystemExit(f'unexpected canonical item count: {len(items)}')
    print(f'CodeUpdate0198 validation passed; canonical_items={len(items)}')
else:
    print('CodeUpdate0198 structural validation passed; canonical item projection not present in changed-files-only overlay')
