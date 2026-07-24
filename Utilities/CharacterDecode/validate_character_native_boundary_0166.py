#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
runtime=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs').read_text()
foundation=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs').read_text()
report=json.loads((ROOT/'Data/NativeDecodeReports/CodeUpdate0166CharacterNativeBoundary/character_native_dependency_boundary_0166.json').read_text())
checks={
 'version':'ContractVersion = "0166"' in runtime,
 'enum':runtime.count('RuntimeCharacterNativeDependency.')>=12,
 'six-decisions':len(report['dependencies'])==6 and runtime.count('"UNKNOWN", false')==6,
 'all-unknown':all(x['classification']=='UNKNOWN' for x in report['dependencies']),
 'all-non-executable':all(x['executable'] is False for x in report['dependencies']),
 'requirements':all(x['evidence_requirement'] for x in report['dependencies']),
 'fallbacks':all(x['fail_closed_behavior'] for x in report['dependencies']),
 'query':'GetNativeDependencyDecision' in runtime and 'GetCharacterNativeDependencyDecision' in foundation,
 'gate':'RequireExecutableNativeDependency' in runtime and 'RequireExecutableCharacterNativeDependency' in foundation,
 'fail-closed':'throw new NotSupportedException' in runtime,
 'generator':all(report['generator_assertions'].values()),
 'classification':report['classification']=='PROVEN'
}
failed=[k for k,v in checks.items() if not v]
if failed: raise SystemExit('failed: '+', '.join(failed))
for path in [ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs',ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs']:
 s=path.read_text()
 if s.count('{')!=s.count('}'): raise SystemExit(f'unbalanced braces: {path}')
print('Character native dependency boundary 0166 validation passed')
