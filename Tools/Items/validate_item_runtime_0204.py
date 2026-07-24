from pathlib import Path
import json, sys
root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
required = [
    'Evidence/Items/CodeUpdate0204/item_native_boundary_semantics.json',
    'Evidence/Items/CodeUpdate0204/provenance.json',
    'Librarium/Policies/runtime_item_native_boundary_policy.json',
    'Librarium/Status/items_semantic_exhaustion_status.json',
    'Reports/DataDecode/items_runtime_semantic_exhaustion_0204.md',
    'Docs/Audits/Items-CodeUpdate0204-Final-PreNative-Exhaustion.md',
]
for rel in required:
    path = root / rel
    if not path.exists():
        raise SystemExit('missing final audit artifact: ' + rel)
for rel in required[:4]:
    json.loads((root / rel).read_text())
evidence = json.loads((root / required[0]).read_text())
status = json.loads((root / required[3]).read_text())
policy = json.loads((root / required[2]).read_text())
if evidence.get('native_only_boundary_reached') is not True:
    raise SystemExit('native boundary evidence not closed')
if status.get('status') != 'native_only_boundary_reached':
    raise SystemExit('status does not declare native-only boundary')
if status.get('classic_semantic_exhaustion_complete') is not False:
    raise SystemExit('status must preserve final native work as incomplete')
if policy.get('pre_native_semantic_exhaustion_complete') is not True:
    raise SystemExit('pre-native completion policy missing')
if len(evidence.get('native_dependency_groups', [])) < 6:
    raise SystemExit('native dependency inventory is incomplete')
items = root / 'Data/Classic/items.json'
if items.exists():
    data = json.loads(items.read_text())
    count = len(data['records'])
    if count != 1950:
        raise SystemExit(f'unexpected canonical item count: {count}')
    if evidence.get('canonical_item_definitions_validated') != count:
        raise SystemExit('evidence canonical count mismatch')
    print(f'CodeUpdate0204 validation passed; canonical_items={count}; native_only_boundary=true')
else:
    print('CodeUpdate0204 structural validation passed; native_only_boundary=true; canonical projection not present')
