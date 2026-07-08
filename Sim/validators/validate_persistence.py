
from __future__ import annotations

from pathlib import Path
import json
import tempfile


def validate_persistence(repo_root: Path) -> dict:
    notes = []
    passed = failed = unknown = 0
    try:
        from Runtime.world.world_state import WorldState, RuntimeEntity, GroundItem
        from Runtime.abilities.ability_engine import AbilityState, AbilityGrant
        from Runtime.quests.quest_engine import QuestDefinition, QuestObjective, QuestState
        from Runtime.persistence.persistence_runtime import PersistenceRuntime, SaveEnvelope, SAVE_SCHEMA_VERSION

        world = WorldState(world_id="validation_world", tick=42, current_room="room_001")
        player = RuntimeEntity(
            runtime_id="player_validation",
            name="Validation Hero",
            entity_type="player",
            room_ref="room_001",
            hp=25,
            max_hp=30,
            mana=8,
            max_mana=10,
            cp=4,
            max_cp=5,
            level=3,
            stats={"str": 12, "agi": 10},
            skills={"sword": 25},
            inventory=[{"item_id": "test_sword", "name": "Test Sword", "quantity": 1}],
            equipment={"weapon": {"item_id": "test_sword", "name": "Test Sword"}},
            flags={"class_id": "fighter", "race_id": "human"},
        )
        monster = RuntimeEntity(runtime_id="monster_validation", name="Validation Goblin", entity_type="monster", room_ref="room_001", hp=7, max_hp=7)
        world.add_player(player)
        world.add_monster(monster)
        world.ground_items.append(GroundItem(item_id="coin", name="Coin", room_ref="room_001", quantity=3))
        world.spawn_timers["spawn_001"] = 100
        world.combat_pairs[player.runtime_id] = monster.runtime_id
        world.flags["weather"] = "clear"

        abilities = AbilityState()
        abilities.grant(AbilityGrant(ability_id="quest_gate_alpha", source_type="validation", source_id="validator", scope="quest"))
        quest = QuestDefinition(
            quest_id="validation_quest",
            name="Validation Quest",
            reward_abilities=["quest_complete_alpha"],
            objectives=[QuestObjective(objective_id="talk", kind="talk", target="npc", required_count=1)],
        )
        quests = QuestState()
        quests.start(quest, abilities)
        quests.record_objective(quest, "talk", 1)
        quests.complete(quest, abilities)

        with tempfile.TemporaryDirectory() as tmp:
            runtime = PersistenceRuntime(Path(tmp))
            char_path = runtime.save_character(player, ability_state=abilities, quest_state=quests)
            world_path = runtime.save_world(world)
            snap_path = runtime.save_snapshot("validation_snapshot", world)
            loaded_player = runtime.load_character(player.runtime_id)
            loaded_world = runtime.load_world(world.world_id)

            for path in [char_path, world_path, snap_path]:
                data = json.loads(Path(path).read_text(encoding="utf-8"))
                if data.get("schema_version") == SAVE_SCHEMA_VERSION and data.get("checksum"):
                    passed += 1
                else:
                    failed += 1; notes.append(f"Save envelope invalid: {path}")

            if loaded_player.runtime_id == player.runtime_id and loaded_player.inventory[0]["item_id"] == "test_sword":
                passed += 1
            else:
                failed += 1; notes.append("Character round-trip failed")

            if loaded_world.world_id == world.world_id and loaded_world.tick == 42 and "player_validation" in loaded_world.players and loaded_world.ground_items[0].item_id == "coin":
                passed += 1
            else:
                failed += 1; notes.append("World round-trip failed")

            env = SaveEnvelope.from_dict(json.loads(Path(char_path).read_text(encoding="utf-8")))
            if env.classicAccuracy == "reforged_runtime_persistence_not_classic_binary_claim":
                passed += 1
            else:
                failed += 1; notes.append("Persistence provenance/classic accuracy tag missing")

        status = "PASS" if failed == 0 else "FAIL"
    except Exception as exc:
        status = "ERROR"
        failed += 1
        notes.append(str(exc))
    return {"status": status, "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
