from pathlib import Path
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
models=(root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text()
services=(root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text()
vm=(root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text()
xaml=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
checks={
'endpoint model':'class RealmHostEndpoint' in models,
'persistent collection':'RemoteHosts' in models,
'http validation':'Uri.UriSchemeHttp' in services and 'Uri.UriSchemeHttps' in services,
'timeout bound':'Math.Clamp(endpoint.TimeoutSeconds, 1, 60)' in services,
'env token':'GetEnvironmentVariable(endpoint.ApiTokenEnvironmentVariable)' in services,
'no persisted token value':'ApiTokenEnvironmentVariable' in models and 'ApiToken ' not in models,
'bearer auth':'AuthenticationHeaderValue("Bearer"' in services,
'unreachable not connected':'new(endpoint.Name, false' in services,
'remote merge':'remotes.Count(x => x.Connected)' in services,
'add remove commands':'AddRemoteHostCommand' in vm and 'RemoveRemoteHostCommand' in vm,
'editable grid':'RealmKeeper.RemoteHosts' in xaml.read_text(),
'schema persisted':'public int SchemaVersion' in models and 'state.SchemaVersion =' in services,
}
ET.parse(xaml)
failed=[k for k,v in checks.items() if not v]
for k,v in checks.items(): print(('PASS' if v else 'FAIL')+': '+k)
print('PASS: XAML parse')
if failed: raise SystemExit(1)
print(f'PASS {len(checks)+1}/{len(checks)+1}')
