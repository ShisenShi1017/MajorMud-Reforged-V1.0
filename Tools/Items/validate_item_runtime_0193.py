from pathlib import Path
import json,sys
root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
source=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
smoke=(root/'Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs').read_text()
for token in ['SaveValidatedWithIntegrityBackup','LoadValidatedWithIntegrityRecovery','BackupIntegrityPath','RestoreIntegrityBackupAtomically']:
    if token not in source: raise SystemExit('missing runtime reference: '+token)
for token in ['validated-world-item-integrity-backup-recovery','tampered integrity backup was accepted']:
    if token not in smoke: raise SystemExit('missing smoke coverage: '+token)
for rel in ['Evidence/Items/CodeUpdate0193/item_world_persistence_integrity_recovery_semantics.json','Evidence/Items/CodeUpdate0193/provenance.json','Librarium/Policies/runtime_item_world_persistence_integrity_recovery_policy.json']:
    json.loads((root/rel).read_text())
items=json.loads((root/'Data/Classic/items.json').read_text())['records']
print(f'CodeUpdate0193 validation passed; canonical_items={len(items)}')
