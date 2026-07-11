import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
required=[
 root/'Apps/Source/Reforged.Server/RuntimeCombat.cs',
 root/'Apps/Source/Reforged.Server/RuntimeCombatAdvanced.cs',
 root/'Apps/Source/Reforged.Server/RuntimeCombatPart2SmokeReport.cs',
]
errors=[f'missing:{p.relative_to(root)}' for p in required if not p.exists()]
if not errors:
 text=(root/'Apps/Source/Reforged.Server/RuntimeCombatAdvanced.cs').read_text(encoding='utf-8')
 for token in ['IRuntimeCombatThreatPolicy','IRuntimeCombatAiPolicy','IRuntimeCombatRoundEffectSource','IRuntimeCombatRewardHandler','RuntimeCombatAdvancedSnapshot','ExecuteNpcTurns','ApplyRewardsIfComplete']:
  if token not in text: errors.append(f'missing-token:{token}')
print(json.dumps({'passed':not errors,'errors':errors},indent=2))
raise SystemExit(0 if not errors else 1)
