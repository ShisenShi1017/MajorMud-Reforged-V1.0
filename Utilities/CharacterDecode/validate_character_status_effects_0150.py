#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
contract=json.loads((root/'Data/NativeDecodeReports/CodeUpdate0150CharacterStatus/character_status_lifecycle_contract_0150.json').read_text())
assert contract['promotion']['replacement']=='none_independent_instances'
assert contract['promotion']['timer_owner']=='runtime_spell_state'
status=(root/'Apps/Source/Reforged.Server/RuntimeCharacterStatusEffects.cs').read_text()
semantic=(root/'Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs').read_text()
spells=(root/'Apps/Source/Reforged.Server/RuntimeSpells.cs').read_text()
for token in ['ReplacementPolicyNone', 'TimerOwnerWorldSpellLedger', 'preservedStatuses', 'preservedTimers', 'ProjectionOrdinal']:
    assert token in status or token in semantic, token
assert 'CurrentSaveVersion = 6' in semantic
for token in ['OrderBy(x => x.AppliedAtUtc)', 'ThenBy(x => x.EffectId', 'Normalize(RuntimeSpellState state)']:
    assert token in spells, token
assert 'SaveVersion = Math.Max(character.SaveVersion, 6)' in status
print('PASS CodeUpdate0150 Character status effects and timers phase 2')
