from pathlib import Path
import json, sys
root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
source = (root / 'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke = (root / 'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in [
    'RuntimeItemLimitedReconciliationCoordinator',
    'RuntimeItemLimitedReconciliationReport',
    'maximum_world_quantity',
    'OWNER_LIMIT_EXCEEDED',
    'WORLD_LIMIT_EXCEEDED',
    'LoadValidatedAcrossSaveGenerationsAndLimits',
    'SaveValidatedAcrossSaveGenerationsAndLimits'
]:
    if token not in source:
        raise SystemExit('missing runtime reference: ' + token)
for token in [
    'global-limited-item-reconciliation',
    'owner limited-item overflow was not detected',
    'world limited-item overflow was not detected'
]:
    if token not in smoke:
        raise SystemExit('missing smoke coverage: ' + token)
for rel in [
    'Evidence/Items/CodeUpdate0202/item_global_limited_reconciliation_semantics.json',
    'Evidence/Items/CodeUpdate0202/provenance.json',
    'Librarium/Policies/runtime_item_global_limited_reconciliation_policy.json'
]:
    json.loads((root / rel).read_text())
items = root / 'Data/Classic/items.json'
if items.exists():
    count = len(json.loads(items.read_text())['records'])
    if count != 1950:
        raise SystemExit(f'unexpected canonical item count: {count}')
    print(f'CodeUpdate0202 validation passed; canonical_items={count}')
else:
    print('CodeUpdate0202 structural validation passed; canonical item projection not present')
