from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
x=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
t=x.read_text(encoding='utf-8')
checks=[
('xaml parses', lambda: ET.parse(x)),
('five chamber styles', lambda: all(k in t for k in ['RealmKeeperPanel','WandererPanel','AnalystPanel','WorldsmithPanel','ArchivistPanel'])),
('five purpose badges', lambda: all(k in t for k in ['GLOBAL REALM COMMAND','LIVE CHARACTER OPERATIONS','READ-ONLY DATA OBSERVATORY','APPROVAL-GATED TEST REALM','PREVIEW-ONLY EVIDENCE WORKBENCH'])),
('forge remains separate', lambda: 'ForgeGuardianView' not in t),
]
failed=[]
for name,fn in checks:
 try: fn(); print('PASS',name)
 except Exception as e: failed.append((name,str(e))); print('FAIL',name,e)
print(f'{len(checks)-len(failed)}/{len(checks)} checks passed')
sys.exit(1 if failed else 0)
