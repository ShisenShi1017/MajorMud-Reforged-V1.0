#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
contract=json.loads((ROOT/'Data/NativeDecodeReports/CodeUpdate0160CharacterDeath/character_death_contract_0160.json').read_text())
assert contract['classification']=='STRONGLY CORRELATED'
death=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterDeath.cs').read_text()
combat=(ROOT/'Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs').read_text()
semantic=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs').read_text()
for token in ['public static class RuntimeCharacterDeathResolver','Character death transition has already been applied.','dead_with_recoverable_corpse','respawned_corpse_pending_or_recovered','RuntimeCharacterDeathMarkers.GoldEscrow','RuntimeCharacterDeathMarkers.RetainedRootItems']:
    assert token in death, token
assert 'RuntimeCharacterDeathResolver.Defeat(before, catalog, session.CurrentRoom.Id' in combat
assert 'RuntimeCharacterDeathResolver.Respawn(character, room.Id, room.RoomNumber' in combat
assert 'You must respawn before recovering your corpse.' in combat
assert 'RuntimeCharacterDeathResolver.Validate(character);' in semantic
print('character death 0160 validation passed')
