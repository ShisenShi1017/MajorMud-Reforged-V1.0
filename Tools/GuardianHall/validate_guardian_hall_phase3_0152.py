from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
models = (root / 'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text()
services = (root / 'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text()
view_model = (root / 'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text()
view = (root / 'Apps/Source/Reforged.App/Views/GuardianHallView.xaml').read_text()

checks = {
    'analyst has no character selector': 'class AnalystWorkspace' in models and 'AnalystWorkspace' in models.split('public sealed class WorldsmithWorkspace')[0] and 'public string Character' not in models.split('public sealed class AnalystWorkspace')[1].split('public sealed class WorldsmithWorkspace')[0],
    'analyst telemetry snapshot': 'AnalystTelemetrySnapshot' in models,
    'analyst telemetry service': 'class AnalystTelemetryService' in services,
    'target-specific generated data scan': 'TargetFiles' in services and 'Directory.EnumerateFiles(dataRoot, "*.json"' in services,
    'read-only no mutation status': 'no character control or runtime mutation' in services,
    'analyst refresh command': 'RefreshAnalystCommand' in view_model,
    'analyst initialization refresh': 'await RefreshAnalystAsync(false)' in view_model,
    'measured analyst export': 'Source Records: {Analyst.SourceRecords}' in view_model,
    'analyst telemetry UI': 'Refresh Analysis Data' in view and 'Analyst.SourceRecords' in view,
}
try:
    ET.parse(root / 'Apps/Source/Reforged.App/Views/GuardianHallView.xaml')
    checks['xaml parse'] = True
except Exception:
    checks['xaml parse'] = False

for name, passed in checks.items():
    print(('PASS' if passed else 'FAIL') + ': ' + name)
sys.exit(0 if all(checks.values()) else 1)
