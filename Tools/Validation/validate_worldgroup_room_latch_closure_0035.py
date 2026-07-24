#!/usr/bin/env python3
from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
e=json.loads((root/'Librarium/Evidence/worldgroup_room_latch_closure_0035.json').read_text())
assert e['distinct_effect_count']==7
assert e['exit_row_count']==12
assert e['route_replay_command_hits']==0
assert e['runtime_policy']['player_command_promoted'] is False
assert len(set(e['shared_native_state_ids']))==7
assert all(r['classification']=='native_or_passive_room_effect' for r in e['exit_records'])
assert all(r['player_command_promoted'] is False for r in e['exit_records'])
cs=(root/'Apps/Source/Reforged.Server/WorldgroupRoomLatchClosureRegistry.cs').read_text()
for token in ['DistinctNativePassiveEffects = 7','AffectedExitRows = 12','PromotePlayerCommands = false','closed_hidden']:
    assert token in cs
print('Worldgroup Room latch closure Update0035 validation passed')
