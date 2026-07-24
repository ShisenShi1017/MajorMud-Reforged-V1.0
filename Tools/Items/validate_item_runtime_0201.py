from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['RuntimeItemSaveGenerationSnapshot','RuntimeItemSaveGenerationCoordinator','LoadValidatedAcrossSaveGenerations','SaveValidatedAcrossSaveGenerations','SUBSYSTEM_GENERATION_MISMATCH','ITEM_GENERATION_MISMATCH']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['coordinated-save-generation-recovery','matching coordinated save generations were rejected','stale subsystem generation was not detected']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0201/item_coordinated_save_generation_semantics.json','Evidence/Items/CodeUpdate0201/provenance.json','Librarium/Policies/runtime_item_coordinated_save_generation_policy.json']:
    json.loads((root/rel).read_text())
items=root/'Data/Classic/items.json'
if items.exists():
    n=len(json.loads(items.read_text())['records'])
    if n!=1950: raise SystemExit(f'unexpected canonical item count: {n}')
    print(f'CodeUpdate0201 validation passed; canonical_items={n}')
else: print('CodeUpdate0201 structural validation passed; canonical item projection not present')
