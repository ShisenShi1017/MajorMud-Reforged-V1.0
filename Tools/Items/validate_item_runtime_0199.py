from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['RuntimeItemContainerOwnerReconciliationResult','ReconcileContainerOwnerMismatches','CONTAINER_OWNER_MISMATCH','container_owner_recovery','inherit_authoritative_parent_owner']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['container-owner-mismatch-reconciliation','child did not inherit authoritative parent owner','ownerless parent mismatch was silently reconciled']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0199/item_container_owner_reconciliation_semantics.json','Evidence/Items/CodeUpdate0199/provenance.json','Librarium/Policies/runtime_item_container_owner_reconciliation_policy.json']:
    json.loads((root/rel).read_text())
items_path=root/'Data/Classic/items.json'
if items_path.exists():
    items=json.loads(items_path.read_text())['records']
    if len(items)!=1950: raise SystemExit(f'unexpected canonical item count: {len(items)}')
    print(f'CodeUpdate0199 validation passed; canonical_items={len(items)}')
else:
    print('CodeUpdate0199 structural validation passed; canonical item projection not present in changed-files-only overlay')
