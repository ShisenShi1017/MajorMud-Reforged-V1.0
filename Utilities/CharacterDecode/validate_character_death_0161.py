#!/usr/bin/env python3
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
contract = json.loads((ROOT/'Data/NativeDecodeReports/CodeUpdate0161CharacterDeath/character_death_contract_0161.json').read_text())
death = (ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterDeath.cs').read_text()
items = (ROOT/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
combat = (ROOT/'Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs').read_text()
assert contract['classification'] == 'STRONGLY CORRELATED'
for token in [
    'public const string RecoveredAtUtc',
    'public const string RecoveredState',
    'RuntimeCorpseRecoveryResult recovery',
    'The active death event has no pending recoverable corpse.',
    'engine.RecoverOwnedCorpse(corpseId, character.CharacterId)',
    'RuntimeCharacterDeathMarkers.RecoveredGold'
]: assert token in death, token
for token in [
    'public sealed record RuntimeCorpseRecoveryResult',
    'RecoverOwnedCorpse(string corpseId, string characterId)',
    'Corpse ownership does not match the recovering character.',
    'CleanedUp = true'
]: assert token in items, token
assert 'RuntimeCharacterDeathResolver.RecoverCorpse(character, catalog, session.CurrentRoom.Id' in combat
assert '.OrderByDescending(x => x.CreatedAtUtc)' not in combat
print('character death 0161 validation passed')
