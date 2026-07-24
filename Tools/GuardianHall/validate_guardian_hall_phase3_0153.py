from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
checks={
 'worldsmith model runtime fields': 'TargetIsSafe' in (root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text(),
 'worldsmith runtime service': 'class WorldsmithRuntimeService' in (root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(),
 'safe target enforcement': 'Blocked: Worldsmith requires a Test, Development, or Offline Projection realm.' in (root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(),
 'proposal persistence': 'GenerateProposalAsync' in (root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(),
 'simulation persistence': 'SimulateAsync' in (root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(),
 'no live runtime apply': 'runtimeMutation = false' in (root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(),
 'viewmodel wiring': 'RefreshWorldsmithAsync' in (root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text(),
 'worldsmith refresh command': 'RefreshWorldsmithCommand' in (root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text(),
 'worldsmith UI telemetry': 'Worldsmith.BaselineRecords' in (root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml').read_text(),
}
try: ET.parse(root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'); checks['xaml parse']=True
except Exception: checks['xaml parse']=False
for k,v in checks.items(): print(('PASS' if v else 'FAIL')+': '+k)
failed=[k for k,v in checks.items() if not v]
print(f"RESULT: {len(checks)-len(failed)}/{len(checks)} checks passed")
sys.exit(1 if failed else 0)
