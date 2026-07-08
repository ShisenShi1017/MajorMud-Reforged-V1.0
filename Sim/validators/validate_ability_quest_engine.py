import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)

from Runtime.abilities import AbilityCatalog, AbilityGrant, AbilityState, build_ability_requirement, check_ability_requirement
from Runtime.quests import QuestCatalog, QuestDefinition, QuestObjective, QuestState


def _load_json(path):
    with open(path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)


def validate_ability_quest_engine(repo_root=None):
    passed = 0
    failed = 0
    unknown = 0
    notes = []
    base = repo_root or ROOT
    data_dir = os.path.join(base, 'Data')

    try:
        abilities = _load_json(os.path.join(data_dir, 'abilities.json'))
        catalog = AbilityCatalog.from_records(abilities)
        if len(catalog.by_id) >= 1:
            passed += 1
            notes.append(f'Loaded ability catalog with {len(catalog.by_id)} abilities.')
        else:
            failed += 1
            notes.append('Ability catalog loaded empty.')

        sample = next(iter(catalog.by_id.values()))
        state = AbilityState()
        event = state.grant(AbilityGrant(sample.ability_id, 'validator', '0013B', scope='temporary', expires_at_tick=10))
        if state.has(sample.ability_id, tick=1) and event['event'] == 'ability_granted':
            passed += 1
        else:
            failed += 1
            notes.append('Ability grant/query failed.')

        state.expire(10)
        if not state.has(sample.ability_id, tick=10):
            passed += 1
        else:
            failed += 1
            notes.append('Ability expiration failed.')

        req = build_ability_requirement([sample.ability_id, 'missing_ability_flag'], catalog)
        if sample.ability_id in req['required_ids'] and req['unknown_refs']:
            passed += 1
        else:
            failed += 1
            notes.append('Ability requirement mapping failed.')

        state.grant(AbilityGrant(sample.ability_id, 'validator', '0013B'))
        check = check_ability_requirement(state, [sample.ability_id])
        if check['allowed']:
            passed += 1
        else:
            failed += 1
            notes.append('Ability requirement check failed.')

        # quests.json is currently empty in the canonical data, so validate engine behavior
        # using a synthetic validator quest without promoting it into /Data.
        synthetic_quest = QuestDefinition(
            quest_id='validator_quest_0013C',
            name='Validator Quest',
            required_abilities=[sample.ability_id],
            reward_abilities=[sample.ability_id],
            objectives=[QuestObjective('obj1', 'collect', 'token', 1)],
            implementation_status='validator_only'
        )
        quest_catalog = QuestCatalog([synthetic_quest])
        q = quest_catalog.resolve('validator_quest_0013C')
        if q and q.quest_id == synthetic_quest.quest_id:
            passed += 1
        else:
            failed += 1
            notes.append('Quest catalog resolve failed.')

        quests = QuestState()
        started = quests.start(synthetic_quest, state)
        progressed = quests.record_objective(synthetic_quest, 'obj1', 1)
        completed = quests.complete(synthetic_quest, state)
        if started['event'] == 'quest_started' and progressed['ready'] and completed['event'] == 'quest_completed':
            passed += 1
        else:
            failed += 1
            notes.append('Quest lifecycle failed.')

        actual_quests_path = os.path.join(data_dir, 'quests.json')
        actual_quests = _load_json(actual_quests_path) if os.path.exists(actual_quests_path) else []
        if not actual_quests:
            unknown += 1
            notes.append('Canonical quests.json is empty; quest engine validated with synthetic non-promoted quest fixture.')

    except Exception as exc:
        failed += 1
        notes.append(f'Ability/quest validator error: {exc}')

    return {
        'status': 'PASS' if failed == 0 else 'FAIL',
        'passed': passed,
        'failed': failed,
        'unknown': unknown,
        'notes': notes,
    }
