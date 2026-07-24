from pathlib import Path
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
checks=[]
def ck(name, cond):
 checks.append((name,bool(cond))); print(('PASS' if cond else 'FAIL')+': '+name)
models=(root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text()
services=(root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text()
vm=(root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text()
xaml_path=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
xaml=xaml_path.read_text()
ck('search result model','EvidenceSearchResult' in models and 'SearchResults' in models and 'SearchStatus' in models)
ck('indexed decoded evidence search','SearchEvidence(ArchivistWorkspace' in services and 'item.Findings' in services and 'item.Sha256' in services)
ck('multi-term matching','terms.All' in services and 'StringComparison.OrdinalIgnoreCase' in services)
ck('bounded result set','.Take(250)' in services)
ck('relationship detail','BuildRelationshipDetail' in services and 'Referenced by:' in services)
ck('search commands','SearchEvidenceCommand' in vm and 'ClearEvidenceSearchCommand' in vm)
ck('selection drives relationship viewer','SelectedEvidenceResult' in vm and 'BuildRelationshipDetail' in vm)
ck('search UI','Archivist.SearchResults' in xaml and 'Indexed Evidence Search' in xaml)
ck('read-only results','IsReadOnly="True"' in xaml)
ck('preview-only retained','preview-only' in services.lower() and 'Librarium is never overwritten' in models)
try: ET.parse(xaml_path); ok=True
except Exception: ok=False
ck('xaml parse',ok)
print(f"RESULT: {sum(v for _,v in checks)}/{len(checks)}")
raise SystemExit(0 if all(v for _,v in checks) else 1)
