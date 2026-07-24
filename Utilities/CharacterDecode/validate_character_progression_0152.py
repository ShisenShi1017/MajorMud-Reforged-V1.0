from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
required=[
 'Apps/Source/Reforged.Server/RuntimeCharacterProgression.cs',
 'Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs',
 'Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs',
 'Data/NativeDecodeReports/CodeUpdate0152CharacterProgression/character_level_progression_contract_0152.json',
 'Librarium/Evidence/Character/CHARACTER_LEVEL_PROGRESSION_PASS_0152.md',
 'Librarium/Reports/character_semantic_exhaustion_status_0152.json']
for rel in required:
 p=root/rel; assert p.is_file() and p.stat().st_size>0, rel
contract=json.loads((root/required[3]).read_text())
assert contract['authority']=='Librarium'
assert contract['promoted']['hp_growth']['classification']=='proven'
assert 'never synthesize' in contract['fallback']
progression=(root/required[0]).read_text()
for token in ['RandomNumberGenerator.GetInt32','CalculatePromotionGrowth','MaximumHitPointDelta','MaximumManaDelta','forcedHitPointRoll','checked(']: assert token in progression, token
derived=(root/required[1]).read_text()
for token in ['RuntimeCharacterPromotionGrowth','hp.MinimumPerLevel','race.HitPointsPerLevelAdjustment','constitutionDelta * 2','magic.Level * 2','{ Type: 5 } => 1']: assert token in derived, token
world=(root/required[2]).read_text()
for token in ['GetCatalog(character.WorldId)','RequireRace(character.RaceId)','RequireClass(character.ClassId)']: assert token in world, token
print('character progression 0152 validation passed')
