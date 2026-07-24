from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
models = (root / 'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text(encoding='utf-8')
services = (root / 'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text(encoding='utf-8')
vm = (root / 'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text(encoding='utf-8')
xaml_path = root / 'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
xaml = xaml_path.read_text(encoding='utf-8')

checks = [
    ('XAML parses', lambda: ET.parse(xaml_path)),
    ('schema version 7', lambda: require('SchemaVersion { get; set; } = 7' in models and 'state.SchemaVersion = 7' in services)),
    ('atomic persistence temp file', lambda: require('guardian-hall-state.json' in services and 'tempPath = _path + ".tmp"' in services and 'File.Move(tempPath, _path, true)' in services)),
    ('persistence backup recovery', lambda: require('backupPath = _path + ".backup"' in services and 'Recovered Guardian Hall state from atomic backup' in services)),
    ('diagnostics workspace persisted', lambda: require('GuardianHallDiagnosticsWorkspace' in models and 'Diagnostics { get; set; } = new();' in models)),
    ('five-role smoke check', lambda: require('Distinct().Count() == 5' in services)),
    ('Wanderer character invariant', lambda: require('Wanderer retains a character assignment' in services)),
    ('Worldsmith live isolation invariant', lambda: require('Worldsmith is isolated from Live Realm' in services)),
    ('Archivist preview-only invariant', lambda: require('Archivist Librarium remains preview-only' in services)),
    ('connector bounds checks', lambda: require('TimeoutSeconds is >= 1 and <= 60' in services and 'SessionTimeoutSeconds is >= 1 and <= 60' in services)),
    ('scheduler safety check', lambda: require('Worldsmith has no unattended running job' in services)),
    ('atomic persistence round trip', lambda: require('Atomic persistence round-trip succeeded' in services and 'await _store.LoadAsync(token)' in services)),
    ('self-test command wired', lambda: require('RunHardeningChecksCommand' in vm and 'RunHardeningChecksAsync' in vm)),
    ('self-test UI exposed', lambda: require('Run Hall Self-Test' in xaml and 'Diagnostics.OverallStatus' in xaml and 'Diagnostics.Results' in xaml)),
]

def require(condition):
    if not condition:
        raise AssertionError('required implementation marker missing')

failed = []
for name, fn in checks:
    try:
        fn()
        print('PASS', name)
    except Exception as exc:
        failed.append((name, str(exc)))
        print('FAIL', name, exc)
print(f'{len(checks)-len(failed)}/{len(checks)} checks passed')
sys.exit(1 if failed else 0)
