from pathlib import Path
import json, sys
root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
source = (root / 'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke = (root / 'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in [
    'RuntimeItemAdministrativeInspection',
    'RuntimeItemAdministrativeMoveResult',
    'RuntimeItemAdministrativeAuditResult',
    'AdministrativeInspect',
    'AdministrativeMove',
    'AdministrativeRepair',
    'AdministrativeAudit',
    'AdministrativeRemove'
]:
    if token not in source:
        raise SystemExit('missing runtime reference: ' + token)
for token in ['administrative-item-runtime', 'administrative inspection returned the wrong item', 'administrative removal did not remove the item']:
    if token not in smoke:
        raise SystemExit('missing smoke coverage: ' + token)
for rel in [
    'Evidence/Items/CodeUpdate0203/item_administration_semantics.json',
    'Evidence/Items/CodeUpdate0203/provenance.json',
    'Librarium/Policies/runtime_item_administration_policy.json'
]:
    json.loads((root / rel).read_text())
items = root / 'Data/Classic/items.json'
if items.exists():
    count = len(json.loads(items.read_text())['records'])
    if count != 1950:
        raise SystemExit(f'unexpected canonical item count: {count}')
    print(f'CodeUpdate0203 validation passed; canonical_items={count}')
else:
    print('CodeUpdate0203 structural validation passed; canonical item projection not present')
