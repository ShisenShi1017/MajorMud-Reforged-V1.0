import json
from pathlib import Path
p=Path(__file__).resolve().parents[2]
a=json.loads((p/'Reports/WorldgroupNative/worldgroup_command_import_boundary_0009.json').read_text())
assert a['active_dispatch_entry']=='0x00405104'
by_addr={x['address']:x for x in a['corrections']}
assert '_injacr' in by_addr['0x004789CA']['proven_interpretation']
assert '_globs' in by_addr['0x0049D4D8']['proven_interpretation']
assert '_nglobs' in by_addr['0x0049D48C']['proven_interpretation']
assert a['dispatch_loop']['results']['-4'].startswith('continue')
print('worldgroup command import boundary 0009: OK')
