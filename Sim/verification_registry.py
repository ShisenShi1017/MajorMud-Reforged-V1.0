from Sim.validators.validate_data_load import validate_data_load
from Sim.validators.validate_reference_integrity import validate_reference_integrity
from Sim.validators.validate_formula_engine import validate_formula_engine
from Sim.validators.validate_schema_adapters import validate_schema_adapters
from Sim.validators.validate_snapshot_equipment import validate_snapshot_equipment
from Sim.validators.validate_exp_leveling import validate_exp_leveling
from Sim.validators.validate_regeneration import validate_regeneration
from Sim.validators.validate_movement import validate_movement
from Sim.validators.validate_spawn import validate_spawn
from Sim.validators.validate_monster_ai import validate_monster_ai
from Sim.validators.validate_combat import validate_combat
from Sim.validators.validate_loot_economy_npc import validate_loot_economy_npc
from Sim.validators.validate_spells_status import validate_spells_status
from Sim.validators.validate_command_gameloop import validate_command_gameloop
from Sim.validators.validate_class_race_engine import validate_class_race_engine
from Sim.validators.validate_ability_quest_engine import validate_ability_quest_engine
from Sim.validators.validate_persistence import validate_persistence
from Sim.validators.validate_world_integration import validate_world_integration
from Sim.validators.validate_full_game_harness import validate_full_game_harness
from Sim.validators.validate_gameplay_completion import validate_gameplay_completion
from Sim.validators.validate_runtime_placeholders import validate_runtime_placeholders

VALIDATORS = [
    validate_data_load,
    validate_reference_integrity,
    validate_formula_engine,
    validate_schema_adapters,
    validate_snapshot_equipment,
    validate_exp_leveling,
    validate_regeneration,
    validate_movement,
    validate_spawn,
    validate_monster_ai,
    validate_combat,
    validate_loot_economy_npc,
    validate_spells_status,
    validate_command_gameloop,
    validate_class_race_engine,
    validate_ability_quest_engine,
    validate_persistence,
    validate_world_integration,
    validate_full_game_harness,
    validate_gameplay_completion,
    validate_runtime_placeholders,
]
