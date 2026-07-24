from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
files={
'models':root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs',
'services':root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs',
'vm':root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs',
'xaml':root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'}
text={k:p.read_text(encoding='utf-8') for k,p in files.items()}
checks={
'persisted orchestration workspace':'GuardianOrchestrationWorkspace' in text['models'] and 'Orchestration { get; set; }' in text['models'],
'complete job lifecycle':all(x in text['models'] for x in ['Queued','Running','Completed','Failed','Blocked','Paused','Cancelled']),
'single-run gate':'SemaphoreSlim _gate' in text['services'],
'bounded history':'while (orchestration.Jobs.Count > 100)' in text['services'],
'restart recovery':'RecoverInterruptedJobs' in text['vm'] and 'Recovered after application restart' in text['vm'],
'wanderer real-session block':'no real assigned Wanderer session is available' in text['vm'],
'worldsmith unattended block':'Worldsmith operations require manual approval' in text['vm'],
'analyst scheduled export':'await ExportAnalystAsync()' in text['vm'],
'archivist batch execution':'await DecodeAllAsync()' in text['vm'],
'operator cancellation':'CancelRunningJobs' in text['vm'],
'activity center':'Guardian Activity Center' in text['xaml'],
'scheduler controls':all(x in text['xaml'] for x in ['Queue Selected','Run Due Jobs','Pause Scheduler','Cancel Running']),
'schema version persisted':'public int SchemaVersion' in text['models'] and 'state.SchemaVersion =' in text['services']}
try: ET.parse(files['xaml']); checks['xaml parses']=True
except Exception: checks['xaml parses']=False
for name,ok in checks.items(): print(('PASS' if ok else 'FAIL')+': '+name)
failed=[k for k,v in checks.items() if not v]
print(f"RESULT: {'PASS' if not failed else 'FAIL'} {len(checks)-len(failed)}/{len(checks)}")
sys.exit(1 if failed else 0)
