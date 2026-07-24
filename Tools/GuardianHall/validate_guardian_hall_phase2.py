from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
files={
'models':root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs',
'services':root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs',
'vm':root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs',
'xaml':root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml',
'codebehind':root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml.cs'}
for name,p in files.items():
    if not p.exists(): raise SystemExit(f'FAIL missing {name}: {p}')
ET.parse(files['xaml'])
models=files['models'].read_text(encoding='utf-8')
vm=files['vm'].read_text(encoding='utf-8')
xaml=files['xaml'].read_text(encoding='utf-8')
services=files['services'].read_text(encoding='utf-8')
required=['RealmKeeperWorkspace','WandererWorkspace','AnalystWorkspace','WorldsmithWorkspace','ArchivistWorkspace','LocalToolRegistration','GuardianHallState']
for token in required:
    assert token in models, f'FAIL missing model {token}'
for token in ['IsRealmKeeper','IsWanderer','IsAnalyst','IsWorldsmith','IsArchivist','QueueDroppedFiles','ExportCodexAsync']:
    assert token in vm, f'FAIL missing VM behavior {token}'
for token in ['Realm Keeper War Room','Wanderer Expedition Lodge','Analyst Observatory','Worldsmith Design Foundry','Archivist Librarium Workbench','Upload Files','Upload Folder','Export for Codex','Editable Local Tool Registry']:
    assert token in xaml, f'FAIL missing XAML surface {token}'
for token in ['GenerateReportAsync','ExportCodexAsync','LaunchTool','MinimalPdf']:
    assert token in services, f'FAIL missing service {token}'
assert 'Character' not in xaml[xaml.index('Analyst Observatory'):xaml.index('Worldsmith Design Foundry')], 'FAIL Analyst exposes character'
assert 'Character' not in xaml[xaml.index('Worldsmith Design Foundry'):xaml.index('Archivist Librarium Workbench')], 'FAIL Worldsmith exposes character'
print('Guardian Hall Phase 2 static validator: PASS')
