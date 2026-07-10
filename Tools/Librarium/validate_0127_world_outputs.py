#!/usr/bin/env python3
"""Validate the 0127 world-output contract against the current Classic-first architecture.

Historical Custom/AI generated outputs are no longer mandatory. Their Librarium overlay
sources must remain present, and generated world outputs are validated when they exist.
"""
import json
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
errors = []
validated = []

classic = root / 'Data/Classic'
if not classic.exists():
    errors.append(f'missing world output: {classic}')
else:
    for name in ['compile_manifest.json', 'world_manifest.json']:
        if not (classic / name).exists():
            errors.append(f'missing {name} in {classic}')
    if (classic / 'compile_manifest.json').exists():
        manifest = json.loads((classic / 'compile_manifest.json').read_text(encoding='utf-8'))
        code_update = str(manifest.get('code_update', ''))
        if not code_update.isdigit() or int(code_update) < 127:
            errors.append(f'compile manifest predates 0127 in {classic}')
        if manifest.get('object_families_compiled', 0) < 10:
            errors.append(f'too few families in {classic}')
        validated.append(str(classic.relative_to(root)))

# Custom and AI worlds are overlay sources until explicitly compiled by a host workflow.
for overlay in [root/'Librarium/Overlays/CustomTemplate', root/'Librarium/Overlays/AIFromClassic']:
    if not overlay.exists():
        errors.append(f'missing overlay source: {overlay}')

# Validate optional generated outputs if a user has compiled them.
for world in [root/'Data/Worlds/CustomTemplate', root/'Data/Worlds/AIFromClassic']:
    if not world.exists():
        continue
    for name in ['compile_manifest.json', 'world_manifest.json']:
        if not (world/name).exists():
            errors.append(f'missing {name} in optional generated world {world}')
    validated.append(str(world.relative_to(root)))

if not (root/'ImportedDatabases').exists():
    errors.append('ImportedDatabases missing')

if errors:
    print('FAIL')
    print('\n'.join(errors))
    sys.exit(1)
print(json.dumps({'status':'PASS','validated_outputs':validated,'overlay_sources_present':True}, indent=2))
