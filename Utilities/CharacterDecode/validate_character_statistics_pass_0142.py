#!/usr/bin/env python3
import argparse,json,re,sys
from pathlib import Path

def main():
 ap=argparse.ArgumentParser(); ap.add_argument('--stage',required=True); ap.add_argument('--repo',required=True); a=ap.parse_args()
 stage=Path(a.stage); repo=Path(a.repo); errors=[]
 contract=json.loads((stage/'Data/NativeDecodeReports/CodeUpdate0142CharacterStatistics/character_statistics_contract_0142.json').read_text())
 foundation=json.loads((repo/'Data/Classic/character_foundation.json').read_text())
 if contract['canonical_primary_statistics'] != ['str','int','wil','agl','hea','cha']: errors.append('canonical primary statistic order mismatch')
 if contract['tests']['creation_matrix_cases'] != len(foundation['races'])*len(foundation['classes']): errors.append('creation matrix test count mismatch')
 cs=(stage/'Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs').read_text()
 required=['healthAdjustmentProduct + 15','healthAdjustmentProduct >> 4','+ constitutionAdjustment','MaximumBonusPerLevel','Type: 5','Type: >= 1 and <= 4','checked(']
 for token in required:
  if token not in cs: errors.append('runtime formula token missing: '+token)
 f=(stage/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs').read_text()
 if 'RuntimeCharacterDerivedStatisticCalculator.CalculateCreation' not in f: errors.append('creation runtime not integrated')
 for path in stage.rglob('*.json'):
  try: json.loads(path.read_text())
  except Exception as e: errors.append(f'invalid json {path}: {e}')
 for path in stage.rglob('*.py'):
  try: compile(path.read_text(),str(path),'exec')
  except Exception as e: errors.append(f'invalid python {path}: {e}')
 if errors:
  print('\n'.join(errors)); return 1
 print('Character statistics pass 0142 validation passed')
 print('creation matrix cases: 195')
 print('promoted formulas: maximum_hit_points, maximum_mana')
 return 0
if __name__=='__main__': sys.exit(main())
