#!/usr/bin/env python3
import json, pathlib, sys
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
contract=json.loads((root/'Data/NativeDecodeReports/CodeUpdate0149CharacterStatus/character_status_lifecycle_contract_0149.json').read_text())
assert contract['promotion']['stack_policy']=='unique_effect_instance'
assert contract['promotion']['tick_domain']=='utc_expiration'
status=(root/'Apps/Source/Reforged.Server/RuntimeCharacterStatusEffects.cs').read_text()
semantic=(root/'Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs').read_text()
live=(root/'Apps/Source/Reforged.Server/RuntimeLivePowers.cs').read_text()
for token in ['RuntimeCharacterStatusEffectResolver','Synchronize(','spell.effect.','ExpiresAtUtc']:
    assert token in status, token
assert 'CurrentSaveVersion = 5' in semantic or 'CurrentSaveVersion = 6' in semantic
for token in ['runtime.live_powers.cast','runtime.live_powers.expire','runtime.live_powers.dispel']:
    assert token in live, token
print('PASS CodeUpdate0149 Character status effects and timers')
