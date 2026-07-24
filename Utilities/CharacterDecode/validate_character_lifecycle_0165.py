#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
runtime=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs').read_text()
foundation=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs').read_text()
contract=json.loads((ROOT/'Data/NativeDecodeReports/CodeUpdate0165CharacterLifecycle/character_lifecycle_contract_0165.json').read_text())
checks={
 'version':any(f'ContractVersion = \"{v}\"' in runtime for v in ('0165','0166')),
 'sequence':'SequenceMarker' in runtime and 'checked(previousSequence + 1L)' in runtime,
 'transition':'TransitionMarker' in runtime and '|complete' in runtime,
 'timestamp':'event time moved backwards' in runtime,
 'marker-validation':runtime.count('ValidateLifecycleMarkers(')>=3,
 'stale-recovery':'RecoverStaleSession' in runtime and 'RecoverStaleCharacterSession' in foundation,
 'owner-match':'does not own Character' in runtime,
 'same-cleanup-path':'return Logout(character, staleSessionId' in runtime,
 'classification':contract['classification']=='STRONGLY_CORRELATED',
 'unknowns':len(contract['unknown_native_behaviors'])==6,
 'assertions':all(contract['generator_assertions'].values())
}
failed=[k for k,v in checks.items() if not v]
if failed: raise SystemExit('failed: '+', '.join(failed))
for path in [ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs',ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs']:
 s=path.read_text()
 if s.count('{')!=s.count('}'): raise SystemExit(f'unbalanced braces: {path}')
print('Character Lifecycle 0165 validation passed')
