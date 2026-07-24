from pathlib import Path
import sys
root = Path(__file__).resolve().parents[2]
models = (root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text(encoding='utf-8')
services = (root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(encoding='utf-8')
vm = (root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text(encoding='utf-8')
checks = {
 'schema version persisted': 'public int SchemaVersion' in models and 'SchemaVersion = 2' in services and 'state.SchemaVersion =' in services,
 'legacy assignments detected': 'assignments.json' in services and 'MigrateLegacyAssignmentsAsync' in services,
 'legacy backup preserved': '.phase1-backup' in services and 'File.Copy(_legacyPath, backupPath)' in services,
 'migration receipt written': 'migration-phase1-to-role-workspaces.json' in services,
 'one-time migration': 'if (File.Exists(_path))' in services and 'if (!File.Exists(_legacyPath)) return null' in services,
 'wanderer retains character': 'state.Wanderer.Character = character' in services,
 'analyst no character migration': 'case GuardianRole.Analyst' in services and 'state.Analyst.DataSources' in services,
 'worldsmith live realm protected': 'realm.Contains("Live"' in services and '"Development Realm"' in services,
 'realm keeper gets global scope': 'state.RealmKeeper.LocalHost' in services and 'state.RealmKeeper.Classic' in services,
 'archivist migrates to search': 'state.Archivist.SearchText' in services,
 'migration visible in UI': 'state.MigrationStatus.StartsWith' in vm,
 'role profiles retain status': 'profile.Status = ReadEnum' in services,
}
failed=[k for k,v in checks.items() if not v]
for k,v in checks.items(): print(('PASS' if v else 'FAIL')+': '+k)
print(f"RESULT: {'PASS' if not failed else 'FAIL'} {len(checks)-len(failed)}/{len(checks)}")
sys.exit(1 if failed else 0)
