from pathlib import Path
import json, sys
root=Path(__file__).resolve().parents[2]
required=[
 'Apps/Source/Reforged.Server/RuntimeCharacterProgression.cs',
 'Data/NativeDecodeReports/CodeUpdate0151CharacterProgression/character_level_progression_contract_0151.json',
 'Librarium/Evidence/Character/CHARACTER_LEVEL_PROGRESSION_PASS_0151.md',
 'Librarium/Reports/character_semantic_exhaustion_status_0151.json']
for rel in required:
 p=root/rel
 assert p.is_file() and p.stat().st_size>0, rel
contract=json.loads((root/required[1]).read_text())
assert contract['authority']=='Librarium'
assert contract['fallback'].startswith('block promotion')
code=(root/required[0]).read_text()
for token in ['AwardExperience','Promote','checked(','ActiveClassMarker','ClassExperience','ClassLevels']:
 assert token in code, token
semantic=(root/'Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs').read_text()
assert 'CurrentSaveVersion = 7' in semantic
combat=(root/'Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs').read_text()
assert 'RuntimeCharacterProgressionResolver.AwardExperience' in combat
trainer=(root/'Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs').read_text()
assert 'RuntimeCharacterProgressionResolver.Promote' in trainer
print('character progression 0151 validation passed')
