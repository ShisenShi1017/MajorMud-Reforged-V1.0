from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
models=(root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text()
services=(root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text()
vm=(root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text()
xaml=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
checks={
 'archivist persisted decode state': all(x in models for x in ['DecodeStatus','DecodedFiles','FailedFiles','Sha256','Findings']),
 'supported decode pipeline': 'Task DecodeAsync(ArchivistWorkspace workspace' in services,
 'sha256 evidence identity': 'SHA256.HashDataAsync' in services,
 'bounded safe inspection': all(x in services for x in ['ReadBytesCappedAsync','ReadLinesCappedAsync']),
 'structured format inspection': all(x in services for x in ['JsonDocument.ParseAsync','XDocument.Load','CSV/text table']),
 'binary and dll inspection': all(x in services for x in ['Portable executable header','Binary evidence']),
 'preview-only evidence artifacts': 'librariumAction = "preview-only"' in services,
 'save evidence snapshot': 'SaveEvidenceAsync' in services,
 'viewmodel decode commands': all(x in vm for x in ['DecodeSelectedCommand','DecodeAllCommand','SaveEvidenceCommand']),
 'archivist decode UI': all(x in xaml.read_text() for x in ['Decode Selected','Decode Queue','Save Evidence']),
}
try: ET.parse(xaml); checks['xaml parse']=True
except Exception: checks['xaml parse']=False
for k,v in checks.items(): print(('PASS' if v else 'FAIL')+': '+k)
failed=[k for k,v in checks.items() if not v]
print(f"RESULT: {len(checks)-len(failed)}/{len(checks)} checks passed")
sys.exit(1 if failed else 0)
