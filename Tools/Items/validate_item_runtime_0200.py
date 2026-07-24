from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['RuntimeItemOwnershipSnapshot','RuntimeItemCrossSaveOwnershipCoordinator','LoadValidatedAcrossSaves','SaveValidatedAcrossSaves','MISSING_CHARACTER_OWNER','MISSING_NPC_OWNER','MISSING_MERCHANT_OWNER','MISSING_QUEST_REFERENCE','MISSING_EVENT_REFERENCE']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['cross-save-ownership-coordination','valid cross-save ownership snapshot was rejected','missing Character owner was not detected']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0200/item_cross_save_ownership_coordination_semantics.json','Evidence/Items/CodeUpdate0200/provenance.json','Librarium/Policies/runtime_item_cross_save_ownership_coordination_policy.json']:
    json.loads((root/rel).read_text())
items_path=root/'Data/Classic/items.json'
if items_path.exists():
    records=json.loads(items_path.read_text())['records']
    if len(records)!=1950: raise SystemExit(f'unexpected canonical item count: {len(records)}')
    print(f'CodeUpdate0200 validation passed; canonical_items={len(records)}')
else:
    print('CodeUpdate0200 structural validation passed; canonical item projection not present in changed-files-only overlay')
