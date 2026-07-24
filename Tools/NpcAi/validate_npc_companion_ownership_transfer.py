from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
s=(ROOT/'Apps/Source/Reforged.Server/RuntimeNpcCompanionOwnershipTransfer.cs').read_text()
assert 'TransferNpcCompanionOwnership' in s
assert 'current-owner-mismatch' in s
assert 'companion-ownership-transferred' in s
e=json.loads((ROOT/'Evidence/NpcAI/npc_ai_companion_ownership_transfer_evidence.json').read_text())
assert e['update']=='CodeUpdate0204' and e['native_behavior_inferred'] is False
print('npc companion ownership transfer validation passed')
