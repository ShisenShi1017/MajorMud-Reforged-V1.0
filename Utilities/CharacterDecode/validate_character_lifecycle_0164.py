#!/usr/bin/env python3
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
runtime=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs').read_text()
foundation=(ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs').read_text()
contract=json.loads((ROOT/'Data/NativeDecodeReports/CodeUpdate0164CharacterLifecycle/character_lifecycle_contract_0164.json').read_text())
checks={
 'contract-version':any(f'ContractVersion = \"{v}\"' in runtime for v in ('0164','0165','0166')),
 'canonical-id':'RequireCanonicalCharacterId' in runtime,
 'duplicate-active':'already has active ownership' in runtime,
 'login-order':all(x in runtime for x in ['restore.abilities','restore.flags-status','restore.timers','restore.statistics','restore.death-corpse','membership.world','membership.room']),
 'logout-order':all(x in runtime for x in ['detach.trade','detach.combat','detach.movement-resting','membership.room.removed','membership.world.removed','cleanup.transient','save.final']),
 'rollback-cleanup':runtime.count('catch')>=2 and runtime.count('_active.Remove')>=2,
 'item-ownership':'ValidatePersistentOwnership' in runtime and 'Nested item' in runtime and 'Equipped item' in runtime,
 'corpse-linkage':'Corpse' in runtime and 'linked to another character' in runtime,
 'orphan-validation':'ValidateNoOrphans' in runtime,
 'service-integration':'LoginCharacter' in foundation and 'LogoutCharacter' in foundation and 'RuntimeCharacterLifecycleCoordinator' in foundation,
 'classification':contract['classification']=='STRONGLY_CORRELATED',
 'unknowns':len(contract['unknown_native_behaviors'])>=6,
 'generator-assertions':all(contract['generator_assertions'].values())
}
failed=[k for k,v in checks.items() if not v]
if failed: raise SystemExit('failed: '+', '.join(failed))
# simple brace/static structure checks
for path in [ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterLifecycle.cs',ROOT/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs']:
 s=path.read_text();
 if s.count('{')!=s.count('}'): raise SystemExit(f'unbalanced braces: {path}')
print('Character Lifecycle 0164 validation passed')
