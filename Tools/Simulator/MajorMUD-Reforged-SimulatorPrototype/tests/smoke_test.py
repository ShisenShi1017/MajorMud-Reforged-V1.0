from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mmudsim.data import SimData, EvidenceStore
from mmudsim.world import WorldEngine, SpawnState
from mmudsim.player import SimulationSession

def test_loads():
    data = SimData(EvidenceStore(ROOT))
    assert len(data.room_rows) > 0
    assert len(data.nav_rows) > 0
    assert len(data.shop_rows) > 0

def test_world_queries():
    data = SimData(EvidenceStore(ROOT))
    world = WorldEngine(data)
    assert world.room('map1-room1') is not None
    assert isinstance(world.exits('ACCD'), list)
    assert world.shop('65')[1]

def test_spawn_and_combat_projection():
    data = SimData(EvidenceStore(ROOT))
    world = WorldEngine(data)
    spawns = SpawnState(world).spawn_room('map1-room10')
    assert len(spawns) >= 1
    result = SimulationSession(world).fight_projection('goblin')
    assert result['status'] == 'projected_only_not_certified'
