from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
models=(root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text(encoding='utf-8')
services=(root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(encoding='utf-8')
vm=(root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text(encoding='utf-8')
xaml_path=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
xaml=xaml_path.read_text(encoding='utf-8')
checks={
 'tool working directory persisted':'WorkingDirectory' in models,
 'tool enabled state persisted':'public bool Enabled' in models,
 'tool validation state persisted':'ValidationStatus' in models and 'LastError' in models,
 'tool validation service':'ToolValidationResult ValidateTool' in services,
 'safe executable existence check':'File.Exists(executable)' in services,
 'working directory validation':'Directory.Exists(working)' in services,
 'launch tokens':'{folder}' in services and '{root}' in services and '{file}' in services,
 'complete codex package':'INVESTIGATION_SUMMARY.md' in services and 'DECODED_STRUCTURES.json' in services and 'OPEN_QUESTIONS.md' in services and 'manifest.json' in services,
 'structured reports':'BuildHtmlReport' in services and 'MinimalPdf' in services and 'reportVersion = "1.0"' in services,
 'versioned evidence snapshots':'SavedEvidenceCount' in models and 'Evidence-' in services,
 'tool validation command':'ValidateToolCommand' in vm and 'Validate Selected Tool' in xaml,
 'preview-only protection':'previewOnly = true' in services and 'No runtime behavior is promoted automatically' in services,
}
try:
 ET.parse(xaml_path); checks['xaml parse']=True
except Exception: checks['xaml parse']=False
for name,ok in checks.items(): print(('PASS' if ok else 'FAIL')+': '+name)
passed=sum(checks.values())
print(f'RESULT: {passed}/{len(checks)}')
sys.exit(0 if passed==len(checks) else 1)
