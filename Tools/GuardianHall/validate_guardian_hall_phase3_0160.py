from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
models=(root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text()
services=(root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text()
vm=(root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text()
xaml=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
checks={
 'schema persisted':'public int SchemaVersion' in models and 'state.SchemaVersion =' in services,
 'live connector persisted':'LiveConnectorEnabled' in models and 'SessionBaseUrl' in models,
 'environment token':'ApiTokenEnvironmentVariable' in models and 'Environment.GetEnvironmentVariable' in services,
 'token not serialized separately':'Bearer' in services and 'DefaultRequestHeaders.Authorization' in services,
 'bounded timeout':'Math.Clamp(workspace.SessionTimeoutSeconds, 1, 60)' in services,
 'authenticated telemetry':'CaptureRemoteAsync' in services and 'SessionTelemetryPath' in services,
 'local fallback':'Fallback:' in services and 'CaptureLocal' in services,
 'guarded mission dispatch':'AllowMissionDispatch' in services and 'Permission < GuardianPermission.Interact' in services,
 'structured mission payload':'mission = workspace.Mission' in services and 'PostAsync(workspace.MissionStepPath' in services,
 'viewmodel command':'DispatchWandererMissionCommand' in vm and 'DispatchWandererMissionAsync' in vm,
 'scheduled guarded dispatch':'Wanderer.LiveConnectorEnabled && Wanderer.AllowMissionDispatch' in vm,
 'ui connector controls':'Enable authenticated live connector' in xaml.read_text() and 'Dispatch One Mission Step' in xaml.read_text(),
}
try: ET.parse(xaml); checks['xaml parse']=True
except Exception: checks['xaml parse']=False
for name,ok in checks.items(): print(('PASS' if ok else 'FAIL')+': '+name)
print(f"RESULT: {sum(checks.values())}/{len(checks)}")
sys.exit(0 if all(checks.values()) else 1)
